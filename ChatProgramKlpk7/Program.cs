using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent; // Untuk Dictionary yang thread-safe

namespace ChatServer
{
    class Program
    {
        // Menggunakan ConcurrentDictionary agar aman diakses dari banyak thread sekaligus
        // tanpa perlu 'lock' manual saat menambah/menghapus.
        private static ConcurrentDictionary<string, TcpClient> _clients = new ConcurrentDictionary<string, TcpClient>();

        static async Task Main(string[] args)
        {
            // Tentukan IP Address dan Port untuk server
            int port = 8888;
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"✅ Server started on port {port}. Waiting for connections...");

            while (true)
            {
                // Menerima koneksi client secara asinkron
                TcpClient client = await listener.AcceptTcpClientAsync();

                // Menjalankan task baru untuk setiap client yang terhubung
                // Tanda '_' berarti kita tidak menunggu task ini selesai (fire and forget)
                _ = HandleClientAsync(client);
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            string clientId = ""; // Kita akan isi dengan username client
            NetworkStream stream = client.GetStream();
            var buffer = new byte[4096];
            int bytesRead;

            try
            {
                // Langkah 1: Baca username dari client saat pertama kali terhubung
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) return; // Client disconnect sebelum kirim username

                clientId = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                // Cek jika username sudah dipakai
                if (_clients.ContainsKey(clientId))
                {
                    Console.WriteLine($"[DENIED] Username '{clientId}' is already taken.");
                    byte[] busyMsg = Encoding.UTF8.GetBytes("SYSTEM: Username is already taken. Please reconnect with a different name.");
                    await stream.WriteAsync(busyMsg, 0, busyMsg.Length);
                    client.Close();
                    return;
                }

                // Tambahkan client ke dictionary dan sapa semua orang
                _clients.TryAdd(clientId, client);
                Console.WriteLine($"[CONNECTED] {clientId} connected from {((IPEndPoint)client.Client.RemoteEndPoint).Address}.");
                await BroadcastMessageAsync($"[SYSTEM] {clientId} has joined the chat.", clientId);

                // Langkah 2: Masuk ke loop untuk membaca pesan-pesan selanjutnya
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[MSG] From {clientId}: {message}");

                    // Broadcast pesan dari client ke semua client lain
                    await BroadcastMessageAsync($"{clientId}: {message}", clientId);
                }
            }
            catch (IOException)
            {
                Console.WriteLine($"[INFO] Client {clientId} disconnected (connection closed by client).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] An error occurred with client {clientId}: {ex.Message}");
            }
            finally
            {
                // Pastikan client dihapus dari daftar dan koneksi ditutup
                if (!string.IsNullOrEmpty(clientId))
                {
                    _clients.TryRemove(clientId, out _);
                    await BroadcastMessageAsync($"[SYSTEM] {clientId} has left the chat.", clientId);
                    Console.WriteLine($"[DISCONNECTED] {clientId} has been removed.");
                }
                client.Close();
            }
        }

        private static async Task BroadcastMessageAsync(string message, string senderId)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            foreach (var clientEntry in _clients)
            {
                // Optional: Jangan kirim pesan kembali ke pengirimnya
                // if (clientEntry.Key == senderId) continue;

                try
                {
                    NetworkStream stream = clientEntry.Value.GetStream();
                    await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                }
                catch (Exception ex)
                {
                    // Jika gagal mengirim ke client, anggap dia sudah disconnect
                    Console.WriteLine($"[BROADCAST FAILED] Could not send message to {clientEntry.Key}. Error: {ex.Message}");
                }
            }
        }
    }
}