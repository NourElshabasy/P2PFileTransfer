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
                byte[] requestData = Encoding.ASCII.GetBytes("P2P_APP_HI");
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
                        if (message == "P2P_APP_HI")
                        {
                            string peerIp = remoteEndPoint.Address.ToString();

                            // Safely update the UI thread
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // Add them to list if they are not already there
                                if (!LstPeers.Items.Contains(peerIp))
                                {
                                    LstPeers.Items.Add(peerIp);
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
            TxtStatus.Text = "Status: Listening on port 5000...";
            BtnReceive.IsEnabled = false;

            // Wait for the file to finish downloading, and grab the hash it returns!
            string receivedHash = await Task.Run(() => StartListening());

            // Display the first 8 characters of the hash to prove it matches
            TxtStatus.Text = $"Status: Received! Hash: {receivedHash.Substring(0, 8)}...";
            BtnReceive.IsEnabled = true;
        }

        private string StartListening()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();

            using (TcpClient client = listener.AcceptTcpClient())
            using (NetworkStream networkStream = client.GetStream())
            {
                // We wrap this in a block so the file closes BEFORE we try to hash it
                using (FileStream fileStream = File.Create("received_file.dat")) 
                {
                    networkStream.CopyTo(fileStream);
                }
            } 

            listener.Stop();
            
            // Now that the file is fully saved and closed, generate the fingerprint!
            return GetFileHash("received_file.dat");
        }

        // --- SENDER LOGIC ---
        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (LstPeers.SelectedItem is not string ipAddress)
            {
                MessageBox.Show("Please select a peer from the list first!");
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog();

            if (openFileDialog.ShowDialog() == true) 
            {
                string filePath = openFileDialog.FileName;
                
                // 1. Calculate the hash before sending
                string fileHash = GetFileHash(filePath);

                TxtStatus.Text = "Status: Sending file...";
                BtnSend.IsEnabled = false;
                TransferProgressBar.Value = 0; 

                var progress = new Progress<int>(percent => 
                {
                    TransferProgressBar.Value = percent;
                });

                await Task.Run(() => SendFile(filePath, ipAddress, progress));

                // 2. Display the hash once it finishes sending
                TxtStatus.Text = $"Status: Sent! Hash: {fileHash.Substring(0, 8)}...";
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
                    // Create 8KB bucket to hold chunks of data
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    long totalRead = 0;
                    long fileLength = fileStream.Length; // Get total file size

                    // Keep scooping 8KB chunks until the file is empty
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        // Pour the chunk into the network
                        networkStream.Write(buffer, 0, bytesRead);

                        // Keep track of how much scooped
                        totalRead += bytesRead;

                        // Calculate the percentage (0 to 100) and send it to the UI
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
                // Convert the raw bytes into a readable hex string
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}