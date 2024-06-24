using EI.SI;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Projeto_TS
{
    public partial class Chat : Form
    {

        private const int PORT = 10000;
        NetworkStream networkStream;
        ProtocolSI protocolSI;
        TcpClient client;
        AesCryptoServiceProvider aes;
        public string textoCifrado;
        public string palavraCifragem;
        private RSACryptoServiceProvider rsa;
        string publickey;
        delegate void NomeCallback(string text);
        bool desconectar = false;

        public Chat()
        {
            InitializeComponent();
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, PORT);
            client = new TcpClient();
            client.Connect(endpoint);
            networkStream = client.GetStream();
            protocolSI = new ProtocolSI();
            rsa = new RSACryptoServiceProvider();
            publickey = rsa.ToXmlString(false);
            messageHandler();
        }

        private string CifrarTexto(string txt)
        {
            //VARIÁVEL PARA GUARDAR O TEXTO DECIFRADO EM BYTES
            byte[] txtDecifrado = Encoding.UTF8.GetBytes(txt);
            //VARIÁVEL PARA GUARDAR O TEXTO CIFRADO EM BYTES
            byte[] txtCifrado;
            //RESERVAR ESPAÇO NA MEMÓRIA PARA COLOCAR O TEXTO E CIFRÁ-LO
            MemoryStream ms = new MemoryStream();
            //INICIALIZAR O SISTEMA DE CIFRAGEM (WRITE)
            CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            //CRIFRAR OS DADOS
            cs.Write(txtDecifrado, 0, txtDecifrado.Length);
            cs.Close();
            //GUARDAR OS DADOS CRIFRADO QUE ESTÃO NA MEMÓRIA
            txtCifrado = ms.ToArray();
            //CONVERTER OS BYTES PARA BASE64 (TEXTO)
            string txtCifradoB64 = Convert.ToBase64String(txtCifrado);
            //DEVOLVER OS BYTES CRIADOS EM BASE64
            return txtCifradoB64;
        }

    

        public void messageHandler()
        {
            Thread thread = new Thread(ThreadHandler);
            thread.Start();
        }

        private void EscreverTextBoxChat(string text)
        {
            try
            {
                if (this.textBoxchat.InvokeRequired)
                {
                    NomeCallback d = new NomeCallback(EscreverTextBoxChat);
                    this.Invoke(d, new object[] { text });
                }
                else
                {
                    this.textBoxchat.AppendText(text + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public void ThreadHandler()
        {
            byte[] ack;

            //Enviar Chave publica para o server
            byte[] chavePublica = protocolSI.Make(ProtocolSICmdType.PUBLIC_KEY, publickey);
            networkStream.Write(chavePublica, 0, chavePublica.Length);

            //Ficar a espera de resposta do server
            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            {
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
            }

            while (client.Connected)
            {
                if (desconectar == true)
                {
                    break;
                }
                else
                {
                    networkStream = client.GetStream();

                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);

                    // "Alteração"/mudança entre a apresentação
                    // da mensagem e o fim da tranmissão.
                    switch (protocolSI.GetCmdType())
                    {
                        case ProtocolSICmdType.DATA:

                            //receber mensagem do servidor
                            string mensagem = protocolSI.GetStringFromData();

                            //Escrever na TextBoxChat a mensagem já decifrada
                            EscreverTextBoxChat(DecifrarTexto(mensagem));
                            break;

                        case ProtocolSICmdType.USER_OPTION_1:
                            //receber palavra que faz a cifragem do servidor
                            palavraCifragem = protocolSI.GetStringFromData();

                            ack = protocolSI.Make(ProtocolSICmdType.ACK);
                            networkStream.Write(ack, 0, ack.Length);

                            //gera a chave usando palavraCifragem que vem encriptada e esta é desencriptada
                            GerarChave(decifrarRSA(palavraCifragem));
                            break;
                    }
                }
            }
        }
        private string GerarIv(string pass)
        {
            byte[] salt = new byte[] { 7, 8, 7, 8, 2, 5, 9, 5 };
            Rfc2898DeriveBytes pwdGen = new Rfc2898DeriveBytes(pass, salt, 1000);
            //GERAR UMA KEY
            byte[] iv = pwdGen.GetBytes(16);
            aes.IV = iv;
            //CONVERTER PARA BASE64
            string ivB64 = Convert.ToBase64String(iv);
            //DEVOLVER EM BYTES
            return ivB64;
        }
        private string decifrarRSA(string palavraCifrada)
        {
            byte[] dados = Convert.FromBase64String(palavraCifrada);
            //DECIFRAR DADOS ATRAVÉS DO RSA
            byte[] dadosDec = rsa.Decrypt(dados, true);
            return Encoding.UTF8.GetString(dadosDec);
        }
        private string GerarChavePrivada(string pass)
        {
            // O salt, explicado de seguida tem de ter no mínimo 8 bytes e não
            //é mais do que array be bytes. O array é caracterizado pelo []
            byte[] salt = new byte[] { 0, 1, 0, 8, 2, 9, 9, 7 };
            /* A Classe Rfc2898DeriveBytes é um método para criar uma chave e um vector de inicialização.
				Esta classe usa:
				pass = password usada para derivar a chave;
				salt = dados aleatório usado como entrada adicional. É usado para proteger password.
				1000 = número mínimo de iterações recomendadas
			*/
            Rfc2898DeriveBytes pwdGen = new Rfc2898DeriveBytes(pass, salt, 1000);
            //GERAR KEY
            byte[] key = pwdGen.GetBytes(16);
            aes.Key = key;
            //CONVERTER A PASS PARA BASE64. A BASE64 é um método 
            //para codificação de informação e consequente transferência de dados pela Internet
            string passB64 = Convert.ToBase64String(key);
            //DEVOLVER A PASS EM BYTES
            return passB64;
        }
        public void GerarChave(string txt)
        {
            //INICIALIZAR SERVIÇO DE CIFRAGEM AES
            aes = new AesCryptoServiceProvider();
            //GUARDAR A CHAVE GERADA
            GerarChavePrivada(txt);
            GerarIv(txt);
        }
        private string DecifrarTexto(string txtCifradoB64)
        {
            //VARIÁVEL PARA GUARDAR O TEXTO CIFRADO EM BYTES
            byte[] txtCifrado = Convert.FromBase64String(txtCifradoB64);
            //RESERVAR ESPAÇO NA MEMÓRIA PARA COLOCAR O TEXTO E CIFRÁ-LO
            MemoryStream ms = new MemoryStream(txtCifrado);
            //INICIALIZAR O SISTEMA DE CIFRAGEM (READ)
            CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            //VARIÁVEL PARA GUARDO O TEXTO DECIFRADO
            byte[] txtDecifrado = new byte[ms.Length];
            //VARIÁVEL PARA TER O NÚMERO DE BYTES DECIFRADOS
            int bytesLidos = 0;
            //DECIFRAR OS DADOS
            bytesLidos = cs.Read(txtDecifrado, 0, txtDecifrado.Length);
            cs.Close();
            //CONVERTER PARA TEXTO
            string textoDecifrado = Encoding.UTF8.GetString(txtDecifrado, 0, bytesLidos);
            //DEVOLVER TEXTO DECRIFRADO
            return textoDecifrado;
        }


        private void btnEnviar_Click(object sender, EventArgs e)
        {

            string msg = txtMensagem.Text;
            txtMensagem.Clear();
            msg = msg.Replace(Environment.NewLine, "");

            if (string.IsNullOrEmpty(msg))
            {
                return;
            }
            else
            {
                byte[] dados = Encoding.UTF8.GetBytes(msg);
                byte[] signature;
                using (SHA1 sha1 = SHA1.Create())
                {
                    signature = rsa.SignData(dados, sha1);
                }

                byte[] packet = protocolSI.Make(ProtocolSICmdType.DATA, CifrarTexto(msg));
                networkStream.Write(packet, 0, packet.Length);

                packet = protocolSI.Make(ProtocolSICmdType.USER_OPTION_3, signature);
                networkStream.Write(packet, 0, packet.Length);
            }
            txtMensagem.Focus();
        }

        private void btnDesconectar_Click(object sender, EventArgs e)
        {
            try
            {
                //informa o servidor sobre a desconecção
                byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
                networkStream.Write(eot, 0, eot.Length);

                desconectar = true;

                this.Close();

                login login = new login();
                login.Show();
            }
            catch(Exception ex)
            {
                MessageBox.Show("Erro ao desconectar: " + ex.Message);
            }
        }
    }
}
