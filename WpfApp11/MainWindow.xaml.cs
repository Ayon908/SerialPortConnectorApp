using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Timers;


namespace WpfApp11
{
    public class Packet
    {
        public const int HeaderSize = 1;
        public const int FileLengthSize = 4;
        public const int ChecksumSize = 8;
        public byte[] Header { get; set; }
        public int FileLength { get; set; }
        public byte[] FileData { get; set; }
        public long Checksum { get; set; }

        public string FileName { get; set; }

        public string FileNameLength { get; set; }  

        public byte[] ToByteArray()
        {
            byte[] header = new byte[HeaderSize] { 0xFF };
            byte[] fileNameBytes = Encoding.UTF8.GetBytes(FileName);
            byte[] fileNameLengthBytes = BitConverter.GetBytes(fileNameBytes.Length); 
            byte[] fileLengthBytes = BitConverter.GetBytes(FileData.Length);
            byte[] checksumBytes = BitConverter.GetBytes(Checksum);

            int packetLength = HeaderSize + fileNameLengthBytes.Length + fileNameBytes.Length + FileLengthSize + FileData.Length + ChecksumSize;
            byte[] packetBytes = new byte[packetLength];

            Buffer.BlockCopy(header, 0, packetBytes, 0, header.Length);
            Buffer.BlockCopy(fileNameLengthBytes, 0, packetBytes, HeaderSize, fileNameLengthBytes.Length);
            Buffer.BlockCopy(fileNameBytes, 0, packetBytes, HeaderSize + fileNameLengthBytes.Length, fileNameBytes.Length);
            Buffer.BlockCopy(fileLengthBytes, 0, packetBytes, HeaderSize + fileNameLengthBytes.Length + fileNameBytes.Length, FileLengthSize);
            Buffer.BlockCopy(FileData, 0, packetBytes, HeaderSize + fileNameLengthBytes.Length + fileNameBytes.Length + FileLengthSize, FileData.Length);
            Buffer.BlockCopy(checksumBytes, 0, packetBytes, HeaderSize + fileNameLengthBytes.Length + fileNameBytes.Length + FileLengthSize + FileData.Length, checksumBytes.Length);

            return packetBytes;
        }


    }
    public partial class MainWindow : Window, IDisposable
    {
        public SerialPort _serialPort;
        public string _selectedFilePath = "";
        public bool _isFileSelected;
        public int _remainingDataLength;
        private System.Timers.Timer aTimer;
        private object _dataLock = new object();
        private List<byte> _receivedDataBuffer = new List<byte>();
        public int totalCount = 0;
        public Queue<byte> dataQueue = new Queue<byte>();
        

        public MainWindow()
        {
            InitializeComponent();
            InitializeComPortComboBox();

            connectButton.Click += ConnectButton_Click;
            browseButton.Click += BrowseButton_Click;
            sendButton.Click += SendButton_Click;
        }
        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            SerialPort serialPort = _serialPort;
            if (dataQueue.Count > 0)
            {
                lock (dataQueue)
                {
                    int count = dataQueue.Count; 
                    var buffer = dataQueue.ToArray();
                    serialPort.Write(buffer, 0, buffer.Length);
                    dataQueue.Clear();
                }
                Application.Current.Dispatcher.Invoke((() => messageListBox.Items.Add($"sent successfully")));
            }         
        }
        private void InitializeComPortComboBox()
        {
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                comPortComboBox.Items.Add(port);
            }
        }
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                aTimer = new System.Timers.Timer(100); 
                aTimer.Elapsed += OnTimedEvent;
                aTimer.AutoReset = true;
                string selectedPort = comPortComboBox.SelectedItem.ToString();
                _serialPort = new SerialPort(selectedPort);
                _serialPort.Open();
                _serialPort.DataReceived += SerialPort_DataReceived;
                _receivedDataBuffer.Clear();
                aTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                if (openFileDialog.ShowDialog() == true)
                {
                    _selectedFilePath = openFileDialog.FileName;
                    messageTextBox.Text = Path.GetFileName(_selectedFilePath);
                    _isFileSelected = true;
                    DisplayByteArray(_selectedFilePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void DisplayByteArray(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    byte[] fileBytes = File.ReadAllBytes(filePath);

                    Packet packet = new Packet
                    {
                        Header = new byte[1] { 0xFF }, 
                        FileNameLength = Encoding.UTF8.GetBytes(Path.GetFileName(_selectedFilePath)).Length.ToString(),
                        FileLength = fileBytes.Length,
                        FileData = fileBytes,
                        Checksum = CalculateChecksum(filePath),
                        FileName = Path.GetFileName(_selectedFilePath)
                    };

                    byte[] byteArray = packet.ToByteArray();
                    string byteString = string.Join(" ", byteArray.Select(b => $"0x{b:X2}"));           
                    byteArrayTextBox.Text = byteString;
                }
                else
                {
                    MessageBox.Show("File not found!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            try
            {
          
                if (_isFileSelected)
                {
                    byte[] fileBytes = File.ReadAllBytes(_selectedFilePath);

                    Packet packet = new Packet
                    {
                        Header = new byte[1] { 0xFF }, 
                        FileNameLength = Encoding.UTF8.GetBytes(Path.GetFileName(_selectedFilePath)).Length.ToString(),
                        FileLength = fileBytes.Length,
                        FileData = fileBytes,
                        Checksum = CalculateChecksum(_selectedFilePath),
                        FileName = Path.GetFileName(_selectedFilePath)
                    };
                    var byteArray = packet.ToByteArray();
                    lock (dataQueue)
                    {
                        foreach (var b in byteArray)
                        {
                            dataQueue.Enqueue(b);
                        }
                    }
                    int _remainingDataLength = byteArray.Length;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending data from serial port: {ex.Message}");
            }
        }

        public static void SaveByteArrayToFileWithStaticMethod(byte[] data, string filePath)
        {
            File.WriteAllBytes(filePath, data);

        }

        private byte[] accumulatedBytes = new byte[0];
        private object _lockObject = new object();

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                lock (_lockObject)
                {
                    string receivedBytes0 = _serialPort.ReadExisting();
                    byte[] receivedBytes = Encoding.UTF8.GetBytes(receivedBytes0);

                    byte[] combinedBytes = new byte[accumulatedBytes.Length + receivedBytes.Length];
                    accumulatedBytes.CopyTo(combinedBytes, 0);
                    receivedBytes.CopyTo(combinedBytes, accumulatedBytes.Length);
                    accumulatedBytes = combinedBytes;

                    // Extract filename length
                    byte[] fileNameLengthBytes = new byte[4];
                    Array.Copy(accumulatedBytes, 1, fileNameLengthBytes, 0, 4);
                    int fileNameLength = BitConverter.ToInt32(fileNameLengthBytes, 0);

                    // Extract filename
                    byte[] fileNameBytes = new byte[fileNameLength];
                    Array.Copy(accumulatedBytes, 1 + 4, fileNameBytes, 0, fileNameLength);
                    string fileName = Encoding.UTF8.GetString(fileNameBytes);

                    var filePath = "C:\\Users\\Abiswas\\source\\repos\\WpfApp11\\WpfApp11\\bin\\Debug";
                    var fullPath = filePath + Path.DirectorySeparatorChar + fileName;

                    Application.Current.Dispatcher.Invoke(() => messageListBox.Items.Add($"Received complete data: {accumulatedBytes.Length} bytes"));
                    SaveByteArrayToFileWithStaticMethod(accumulatedBytes, fullPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading data from serial port: {ex.Message}");
            }
        }

        private long CalculateChecksum(string filePath)
        {
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                long checksum = 0;

                while (fileStream.Position < fileStream.Length)
                {
                    checksum += fileStream.ReadByte();
                }

                return checksum;
            }
        }
        public void Dispose()
        {
            if (_serialPort != null)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }
        }
    }
}
//Serial Port Connector App
