using Microsoft.Win32;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace P2PFileTransfer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // This will start shouting and listening as soon as the app opens
            Task.Run(() => BroadcastPresence());
            Task.Run(() => ListenForPeers());
        }

        // --- AUTO-DISCOVERY LOGIC ---
        private void BroadcastPresence()
        {
            using (UdpClient udpClient = new UdpClient())
            {
                udpClient.EnableBroadcast = true;
                // Secret handshake message
                string broadcastMessage = "P2P_APP_HI|" + Environment.MachineName;
                byte[] requestData = Encoding.UTF8.GetBytes(broadcastMessage);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, 5001);

                while (true) // Run forever in the background
                {
                    try { udpClient.Send(requestData, requestData.Length, endPoint); } catch { }
                    Thread.Sleep(3000); // Shout our presence every 3 seconds
                }
            }
        }

        private void ListenForPeers()
        {
            using (UdpClient udpListener = new UdpClient(5001))
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 5001);
                while (true) // Listen forever
                {
                    try
                    {
                        byte[] receivedData = udpListener.Receive(ref remoteEndPoint);
                        string message = Encoding.ASCII.GetString(receivedData);

                        // If we hear the secret handshake
                        if (message.StartsWith("P2P_APP_HI|"))
                        {
                            // Split message at | symbol and grab the second part
                            string hostName = message.Split('|')[1];
                            string peerIp = remoteEndPoint.Address.ToString();

                            // Combine into a string for the UI
                            string displayText = $"{hostName} - {peerIp}";

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (!LstPeers.Items.Contains(displayText))
                                {
                                    LstPeers.Items.Add(displayText);
                                }
                            });
                        }
                    }
                    catch { }
                }
            }
        }

        // --- RECEIVER LOGIC ---
        private async void BtnReceive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Status: Listening on port 5000...";
                BtnReceive.IsEnabled = false;

                // The await pulls any errors from the background task so we can safe catch them
                string receivedHash = await Task.Run(() => StartListening());

                TxtStatus.Text = "Status: File received successfully!";
                BtnReceive.IsEnabled = true;
            }
            catch (System.Exception ex)
            {
                // This stops crash and tells us what broke
                MessageBox.Show("Receiver Error: " + ex.Message);

                TxtStatus.Text = "Status: Transfer failed.";
                BtnReceive.IsEnabled = true;
            }
        }

        private string StartListening()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();

            string fullSavePath = "";

            using (TcpClient client = listener.AcceptTcpClient())
            using (NetworkStream networkStream = client.GetStream())
            {
                string fileName = "";
                long expectedLength = 0;

                // We keep the BinaryReader open for the entire process now
                using (BinaryReader reader = new BinaryReader(networkStream, Encoding.UTF8, leaveOpen: true))
                {
                    // Read Metadata
                    fileName = reader.ReadString();
                    expectedLength = reader.ReadInt64();

                    // Create the path
                    string downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    fullSavePath = Path.Combine(downloadsFolder, fileName);

                    // Save the file using the reader's internal buffer
                    using (FileStream fileStream = File.Create(fullSavePath))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        long totalRead = 0;

                        // Scoop bytes using reader.Read()
                        while (totalRead < expectedLength && (bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fileStream.Write(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                        }
                    }
                }
            }

            listener.Stop();
            return GetFileHash(fullSavePath);
        }

        // --- SENDER LOGIC ---
        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (LstPeers.SelectedItem is not string selectedPeer)
            {
                MessageBox.Show("Please select a peer from the list first!");
                return;
            }

            string ipAddress = selectedPeer.Split(" - ")[1];

            OpenFileDialog openFileDialog = new OpenFileDialog();

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                // Calculate hash before sending
                string fileHash = GetFileHash(filePath);

                TxtStatus.Text = "Status: Sending file...";
                BtnSend.IsEnabled = false;
                TransferProgressBar.Value = 0;

                var progress = new Progress<int>(percent =>
                {
                    TransferProgressBar.Value = percent;
                });

                await Task.Run(() => SendFile(filePath, ipAddress, progress));

                // Display the hash once it finishes sending
                TxtStatus.Text = "Status: File sent successfully!";
                BtnSend.IsEnabled = true;
            }
        }

        private void SendFile(string filePath, string ipAddress, IProgress<int> progress)
        {
            try
            {
                using (TcpClient client = new TcpClient(ipAddress, 5000))
                using (NetworkStream networkStream = client.GetStream())
                using (FileStream fileStream = File.OpenRead(filePath))
                {
                    // Send the metadata
                    using (BinaryWriter writer = new BinaryWriter(networkStream, Encoding.UTF8, leaveOpen: true))
                    {
                        // Send the file's name
                        writer.Write(Path.GetFileName(filePath));
                        // Send the total size of the file
                        writer.Write(fileStream.Length);
                    }

                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    long totalRead = 0;
                    long fileLength = fileStream.Length;

                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        networkStream.Write(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        int percentage = (int)((totalRead * 100) / fileLength);
                        progress.Report(percentage);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Transfer failed. Error: " + ex.Message);
                });
            }
        }

        // --- INTEGRITY LOGIC ---
        private string GetFileHash(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                // Convert raw bytes into readable hex string
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}