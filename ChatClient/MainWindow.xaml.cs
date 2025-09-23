using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ChatClient
{
    public partial class MainWindow : Window
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isConnected = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                Disconnect();
                return;
            }

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                MessageBox.Show("Please enter a username.");
                return;
            }

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(IpAddressTextBox.Text, int.Parse(PortTextBox.Text));
                _stream = _client.GetStream();
                _isConnected = true;

                // Kirim username ke server sebagai pesan pertama
                byte[] usernameBytes = Encoding.UTF8.GetBytes(UsernameTextBox.Text);
                await _stream.WriteAsync(usernameBytes, 0, usernameBytes.Length);

                // Jalankan task untuk mendengarkan pesan dari server
                _ = ListenForMessagesAsync();

                UpdateUIOnConnection();
                AppendMessageToChat("✅ Connected to the server!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect: {ex.Message}");
                _isConnected = false;
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected || string.IsNullOrWhiteSpace(MessageInput.Text))
                return;

            try
            {
                string message = MessageInput.Text;
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                await _stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                MessageInput.Clear();
            }
            catch (Exception ex)
            {
                AppendMessageToChat($"[ERROR] Could not send message: {ex.Message}");
            }
        }

        private async Task ListenForMessagesAsync()
        {
            var buffer = new byte[4096];
            try
            {
                while (_isConnected)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        // Server menutup koneksi
                        Dispatcher.Invoke(Disconnect);
                        break;
                    }

                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    AppendMessageToChat(receivedMessage);
                }
            }
            catch (IOException)
            {
                // Terjadi error saat membaca, kemungkinan koneksi terputus
                if (_isConnected) // Hanya disconnect jika kita masih menganggap terhubung
                {
                    Dispatcher.Invoke(Disconnect);
                }
            }
            catch (ObjectDisposedException)
            {
                // Stream/client sudah ditutup, ini normal saat disconnect
            }
        }

        // Helper untuk update UI dari thread manapun
        private void AppendMessageToChat(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ChatArea.AppendText(message + Environment.NewLine);
                ChatArea.ScrollToEnd(); // Auto-scroll ke bawah
            });
        }

        private void Disconnect()
        {
            if (!_isConnected) return;

            _isConnected = false;
            _stream?.Close();
            _client?.Close();

            AppendMessageToChat("🔌 Disconnected from the server.");
            UpdateUIOnDisconnection();
        }

        private void UpdateUIOnConnection()
        {
            ConnectButton.Content = "Disconnect";
            UsernameTextBox.IsEnabled = false;
            IpAddressTextBox.IsEnabled = false;
            PortTextBox.IsEnabled = false;
            SendButton.IsEnabled = true;
            MessageInput.IsEnabled = true;
        }

        private void UpdateUIOnDisconnection()
        {
            ConnectButton.Content = "Connect";
            UsernameTextBox.IsEnabled = true;
            IpAddressTextBox.IsEnabled = true;
            PortTextBox.IsEnabled = true;
            SendButton.IsEnabled = false;
            MessageInput.IsEnabled = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Pastikan koneksi ditutup saat aplikasi ditutup
            Disconnect();
        }
    }
}