using Microsoft.Win32;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Text;
using System.Threading;


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
                // Our secret handshake message
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

                        // If we hear the secret handshake...
                        if (message == "P2P_APP_HI")
                        {
                            string peerIp = remoteEndPoint.Address.ToString();

                            // Safely update the UI thread
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // Add them to the list if they aren't already there
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
            BtnReceive.IsEnabled = false; // Prevent double-clicking

            // Run the listening loop in the background so the UI doesn't freeze
            await Task.Run(() => StartListening());

            TxtStatus.Text = "Status: File received successfully!";
            BtnReceive.IsEnabled = true;
        }

        private void StartListening()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();

            // Wait here until a sender connects
            using (TcpClient client = listener.AcceptTcpClient())
            using (NetworkStream networkStream = client.GetStream())
            using (FileStream fileStream = File.Create("received_file.dat"))
            {
                // Copy all bytes from the network directly into the file
                networkStream.CopyTo(fileStream);
            }

            listener.Stop();
        }

        // --- SENDER LOGIC ---
        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            // This safely checks if it's null AND extracts it as a string variable called ipAddress
            if (LstPeers.SelectedItem is not string ipAddress)
            {
                MessageBox.Show("Please select a peer from the list first!");
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog();

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                // We don't need to grab the IP here anymore, it was grabbed in the 'if' statement above!

                TxtStatus.Text = "Status: Sending file...";
                BtnSend.IsEnabled = false;
                TransferProgressBar.Value = 0; // This will reset the bar to 0%

                // Create a safe messenger to update the UI thread
                var progress = new Progress<int>(percent =>
                {
                    TransferProgressBar.Value = percent;
                });

                // Pass messenger to background task
                await Task.Run(() => SendFile(filePath, ipAddress, progress));

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
    }
}