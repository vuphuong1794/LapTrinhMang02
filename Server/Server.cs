using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading.Tasks;
using System.Threading;

namespace Server
{
    public partial class Server : Form
    {
        private TcpListener listener;
        private List<AuthenticatedStream> clientStreams = new List<AuthenticatedStream>();
        private bool isRunning = false;
        private CancellationTokenSource cancellationTokenSource;

        public Server()
        {
            InitializeComponent();
        }

        private void Server_Load(object sender, EventArgs e)
        {
            // Initialize any necessary components or settings when the form loads
        }

        private async void btn_Start_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs(out IPAddress IP, out int Port))
            {
                return;
            }

            await StartServer(IP, Port);
        }

        private async void btn_Stop_Click(object sender, EventArgs e)
        {
            await NotifyClientsServerStopping();
            StopServer();
        }

        private async void btn_Send_Click(object sender, EventArgs e)
        {
            string message = txt_message.Text;
            if (string.IsNullOrEmpty(message))
            {
                MessageBox.Show("Vui lòng nhập nội dung tin nhắn!", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            await SendMessageToClients("Server: " + message);
        }

        private async Task StartServer(IPAddress IP, int Port)
        {
            listener = new TcpListener(IP, Port);
            listener.Start();
            isRunning = true;
            btn_Start.Enabled = false;
            btn_Send.Enabled = true;
            txt_message.Enabled = true;
            btn_Stop.Enabled = true;
            ShowMessage("Server started...");

            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            try
            {
                while (isRunning)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    ShowMessage("Client connected...");
                    _ = Task.Run(() => HandleClient(client, token), token);
                }
            }
            catch (ObjectDisposedException)
            {
                ShowMessage("Listener stopped.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task HandleClient(TcpClient client, CancellationToken token)
        {
            var stream = new NegotiateStream(client.GetStream(), false);

            try
            {
                await stream.AuthenticateAsServerAsync();
                ShowMessage("Client authenticated.");
                lock (clientStreams)
                {
                    clientStreams.Add(stream);
                }

                byte[] buffer = new byte[512];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    ShowMessage(message);
                    await SendMessageToClients(message);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Client disconnected with error: {ex.Message}");
            }
            finally
            {
                lock (clientStreams)
                {
                    clientStreams.Remove(stream);
                }
                stream.Close();
                client.Close();
                ShowMessage("Client disconnected.");
            }
        }

        private async Task SendMessageToClients(string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            List<AuthenticatedStream> streamsToNotify;

            lock (clientStreams)
            {
                streamsToNotify = new List<AuthenticatedStream>(clientStreams);
            }

            foreach (var stream in streamsToNotify)
            {
                if (stream.CanWrite)
                {
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                }
            }

            ShowMessage("Sent: " + message);
        }

        private async Task NotifyClientsServerStopping()
        {
            string message = "Server is stopping. Disconnecting...";
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            List<AuthenticatedStream> streamsToNotify;

            lock (clientStreams)
            {
                streamsToNotify = new List<AuthenticatedStream>(clientStreams);
            }

            foreach (var stream in streamsToNotify)
            {
                if (stream.CanWrite)
                {
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                }
            }

            ShowMessage("Notified clients that server is stopping.");
        }

        private void StopServer()
        {
            isRunning = false;
            cancellationTokenSource?.Cancel();
            listener?.Stop();

            List<AuthenticatedStream> streamsToClose;
            lock (clientStreams)
            {
                streamsToClose = new List<AuthenticatedStream>(clientStreams);
                clientStreams.Clear();
            }

            foreach (var stream in streamsToClose)
            {
                stream.Close();
            }

            ShowMessage("Server stopped.");
            btn_Start.Enabled = true;
            btn_Send.Enabled = false;
            txt_message.Enabled = false;
            btn_Stop.Enabled = false;
        }

        private bool ValidateInputs(out IPAddress IP, out int Port)
        {
            IP = null;
            Port = 0;

            if (string.IsNullOrEmpty(txt_IP.Text) || string.IsNullOrEmpty(txt_Port.Text))
            {
                MessageBox.Show("Vui lòng nhập IP và Port từ 1000 trở lên!", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!IPAddress.TryParse(txt_IP.Text, out IP))
            {
                MessageBox.Show("Vui lòng nhập đúng địa chỉ IP!");
                return false;
            }

            if (!int.TryParse(txt_Port.Text, out Port))
            {
                MessageBox.Show("Vui lòng nhập Port từ 1000 trở lên!", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private void ShowMessage(string message)
        {
            if (txt_result.InvokeRequired)
            {
                txt_result.Invoke(new Action<string>(doInvoke), message);
            }
            else
            {
                doInvoke(message);
            }
        }

        private void doInvoke(string message)
        {
            txt_result.Text = message + Environment.NewLine + txt_result.Text;
        }

        private void txt_result_TextChanged(object sender, EventArgs e)
        {
        }
    }
}
