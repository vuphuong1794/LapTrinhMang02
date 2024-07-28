using System;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading.Tasks;

namespace Client
{
    public partial class Client : Form
    {
        private TcpClient TCPClient;
        private NegotiateStream negotiateStream;
        private byte[] message;
        private bool isConnected = false;

        public Client()
        {
            InitializeComponent();
        }

        private void Client_Load(object sender, EventArgs e)
        {
            btn_Disconnect.Enabled = false; // Đảm bảo nút ngắt kết nối bị vô hiệu hóa khi tải form
        }

        private async void btn_Connect_Click(object sender, EventArgs e)
        {
            IPAddress IP;
            int Port;

            try
            {
                if (string.IsNullOrEmpty(txt_IP.Text) || string.IsNullOrEmpty(txt_Port.Text))
                {
                    MessageBox.Show("Vui lòng nhập IP và Port từ 1000 trở lên!", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!IPAddress.TryParse(txt_IP.Text, out IP))
                {
                    MessageBox.Show("Vui lòng nhập đúng địa chỉ IP!");
                    return;
                }

                if (!int.TryParse(txt_Port.Text, out Port))
                {
                    MessageBox.Show("Vui lòng nhập Port từ 1000 trở lên!", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                TCPClient = new TcpClient();
                await TCPClient.ConnectAsync(IP, Port);

                if (TCPClient.Connected)
                {
                    negotiateStream = new NegotiateStream(TCPClient.GetStream(), false);
                    await negotiateStream.AuthenticateAsClientAsync();

                    isConnected = true;
                    UpdateUIOnConnect();
                    ShowMessage("Connected and authenticated with server!");

                    // Start reading messages
                    ReadMessages();
                }
                else
                {
                    MessageBox.Show("Không thể kết nối đến server.", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ReadMessages()
        {
            try
            {
                while (isConnected)
                {
                    message = new byte[512];
                    int bytesRead = await negotiateStream.ReadAsync(message, 0, message.Length);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    string strReceived = Encoding.UTF8.GetString(message, 0, bytesRead);
                    ShowMessage(strReceived);

                    // Check if the server is stopping
                    if (strReceived.Contains("Server is stopping. Disconnecting..."))
                    {
                        DisconnectFromServer();
                        MessageBox.Show("Server has stopped. You have been disconnected.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Xử lý trường hợp stream đã bị đóng
                ShowMessage("Connection closed.");
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isConnected = false;
                UpdateUIOnDisconnect();
            }
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

        private async void btn_Send_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txt_message.Text))
            {
                MessageBox.Show("Vui lòng nhập nội dung tin nhắn!", "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                if (negotiateStream != null && negotiateStream.CanWrite && isConnected)
                {
                    byte[] message = Encoding.UTF8.GetBytes("Client: " + txt_message.Text);
                    await negotiateStream.WriteAsync(message, 0, message.Length);
                    ShowMessage("Sent: " + txt_message.Text);
                    txt_message.Clear();
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btn_Disconnect_Click(object sender, EventArgs e)
        {
            DisconnectFromServer();
        }

        private void DisconnectFromServer()
        {
            try
            {
                isConnected = false;

                if (negotiateStream != null)
                {
                    negotiateStream.Close();
                    negotiateStream = null;
                }
                if (TCPClient != null)
                {
                    TCPClient.Close();
                    TCPClient = null;
                }

                UpdateUIOnDisconnect();
                ShowMessage("Disconnected from server.");
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Thông báo!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateUIOnConnect()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateUIOnConnect));
                return;
            }

            btn_Connect.Enabled = false;
            btn_Disconnect.Enabled = true; // Kích hoạt nút ngắt kết nối khi kết nối thành công
            txt_message.Enabled = true;
            btn_Send.Enabled = true;
            txt_IP.Enabled = false;
            txt_Port.Enabled = false;
        }

        private void UpdateUIOnDisconnect()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateUIOnDisconnect));
                return;
            }

            btn_Connect.Enabled = true;
            btn_Disconnect.Enabled = false; // Vô hiệu hóa nút ngắt kết nối khi đã ngắt kết nối
            txt_message.Enabled = false;
            btn_Send.Enabled = false;
            txt_IP.Enabled = true;
            txt_Port.Enabled = true;
        }

        private void Client_FormClosing(object sender, FormClosingEventArgs e)
        {
            DisconnectFromServer();
        }
    }
}
