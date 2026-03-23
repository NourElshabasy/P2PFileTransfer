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
        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Status: Preparing to send...";
            
        }
    }
}