using Microsoft.Win32;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;

namespace P2PFileTransfer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
            OpenFileDialog openFileDialog = new OpenFileDialog();
            
            if (openFileDialog.ShowDialog() == true) 
            {
                string filePath = openFileDialog.FileName;
                string ipAddress = TxtIpAddress.Text;

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