using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Json;
using System.Windows;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Rug.Osc;
using System.Net;
using System.IO;
using Microsoft.Win32;
using System.Text;
using System.Net.Sockets;


namespace HR_BLE_to_UDP
{
    public class SensorTagUuid
    {
        // https://sensortag.codeplex.com/SourceControl/latest#SensorTagLibrary/SensorTagLibrary/Source/SensorTagUuid.cs
        //public const string UuidIrtService = @"180D";
        //public const string UuidIrtString = @"2A37";
        public const string UuidIrtService = "0000180D-0000-1000-8000-00805F9B34FB";
        public const string UuidIrtData = "00002A37-0000-1000-8000-00805F9B34FB";
        // public const string UuidIrtConf = "00002A39-0000-1000-8000-00805F9B34FB";
    }
    public class HeartRate
    {
        public string Device { get; set; }
        public string Timestamp { get; set; }
        public string HR { get; set; }
        public string FHR { get; set; }
        public string Flag { get; set; }
        public string Events { get; set; }
       
    }


    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private GattDeviceService GattDeviceService { get; set; }

        private GattCharacteristic GattCharacteristic { get; set; }
        IPAddress sendAddress = null;
        int count = 0;
        private UdpClient client;
        private float Percent = 1.0f;
        private int FHR;
        private int HR = 60;
        private int chose_event = 0;
        private int flag = 0;
        //１：Set FHR Figure　２：Set Down Percent

        //ファイル書き出し用
        Stream st = null;
        StreamWriter sw = null;
        String delmiter = ",";  //CSVだから

        public MainWindow()
        {
            InitializeComponent();
            //Combobox設定
            cbxType.Items.Add("Set MHR");
            cbxType.Items.Add("Set FHR Figure");
            cbxType.Items.Add("Set Down Percent");

        }

        private async void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            // SensorTagを取得
            var selector = GattDeviceService.GetDeviceSelectorFromUuid(new Guid(SensorTagUuid.UuidIrtService));
            var devices = await DeviceInformation.FindAllAsync(selector);
            var deviceInformation = devices.FirstOrDefault();
            if (deviceInformation == null)
            {
                MessageBox.Show("not found");
                return;
            }

            GattDeviceService = await GattDeviceService.FromIdAsync(deviceInformation.Id);
            MessageBox.Show($"found {deviceInformation.Id}");
        }

        private async void ButtonReadValue_Click(object sender, RoutedEventArgs e)
        {
            if (GattDeviceService == null)
            {
                MessageBox.Show("Please click connect button");
                return;
            }

            // 値を読み始める
            if (GattCharacteristic == null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                GattCharacteristic = GattDeviceService.GetCharacteristics(new Guid(SensorTagUuid.UuidIrtData)).First();
#pragma warning restore CS0618 // Type or member is obsolete
                GattCharacteristic.ValueChanged += GattCharacteristic_ValueChanged;

                var status = await GattCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status == GattCommunicationStatus.Unreachable)
                {
                    MessageBox.Show("Failed");
                }

            }
        }

        private async void GattCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // 値を読んで表示する
            await Dispatcher.InvokeAsync(() =>
            {
                //値を取得
                var data = new byte[args.CharacteristicValue.Length];
                DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);
                HR = int.Parse(data.GetValue(1).ToString());
                if(chose_event != 1)
                {
                    FHR = (int)(HR * Percent);
                }
                //ブラウザ上に送信する心拍数を表示
                textBlock1.Text = "Sample No. " + count.ToString() + "\n HR: " + HR.ToString() + " / FHR: " + FHR.ToString();
                count++;

                if (checkBoxUDP.IsChecked == true)
                {
                    var H = new HeartRate
                    {
                        Device = "Polar",
                        Timestamp = (DateTime.Now.ToString("HH:mm:ss.fff")).ToString(),
                        HR = HR.ToString(),
                        FHR = FHR.ToString(),
                        Flag = flag.ToString(),
                        Events = "0"
                    };

                    using (var ms = new MemoryStream())
                    using (var sr = new StreamReader(ms))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(HeartRate));
                        serializer.WriteObject(ms, H);
                        ms.Position = 0;

                        var json = sr.ReadToEnd();
                        byte[] dgram = Encoding.UTF8.GetBytes(json);
                        client.Send(dgram, dgram.Length);

                    }
                }
                if (sw != null)
                {
                    StringBuilder sb = new StringBuilder();

                    sb.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(delmiter);
                    sb.Append(HR.ToString()).Append(delmiter);
                    sb.Append(FHR.ToString()).Append(delmiter);
                    sb.Append(flag.ToString()).Append(delmiter);
                    sb.Append(Environment.NewLine);

                    sw.Write(sb.ToString());

                }
            });
        }
        //UDP設定
        private void CheckBoxUDP_Checked(object sender, RoutedEventArgs e)
        {
            textBlock1.Text = "open UDP settion";
            sendAddress = IPAddress.Parse(UDP_IPAddress.Text);
            client = new UdpClient();
            client.Connect(sendAddress, Int32.Parse(UDP_Port.Text));

        }
        //UDP終了
        private void CheckBoxUDP_Unchecked(object sender, RoutedEventArgs e)
        {
            textBlock1.Text = "close UDP settion";

        }
        //ファイル作成
        private void SetPath_Click(object sender, RoutedEventArgs e)
        {
            // ファイル保存ダイアログ
            SaveFileDialog dlg = new SaveFileDialog
            {

                // デフォルトファイル名
                //            dlg.FileName = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                FileName = "oh1_D" + DateTime.Now.ToString("yyyyMMdd") + "T" + DateTime.Now.ToString("HHmm"),

                // デフォルトディレクトリ
                InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),

                // ファイルのフィルタ
                Filter = "CSVファイル|*.csv|すべてのファイル|*.*",

                // ファイルの種類
                FilterIndex = 0
            };

            // 指定されたファイル名を取得

            if (dlg.ShowDialog() == true)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("Time").Append(delmiter);
                sb.Append("HR").Append(delmiter);
                sb.Append("FHR").Append(delmiter);
                sb.Append("Flag").Append(delmiter);
                sb.Append(Environment.NewLine);

                st = dlg.OpenFile();
                sw = new StreamWriter(st, Encoding.GetEncoding("UTF-8"));

                sw.Write(sb.ToString());
            }
            else
            {
            }
        }
        //FHR設定
        private void SetPercent_Click (object sender, RoutedEventArgs e)
        {
            if (cbxType.Text.Equals("Set MHR"))
            {
                textBlock1.Text = "Set MHR";
                Percent = 1.0f;
                setPercent.Text = "0";
                chose_event = 2;
            }
            //固定値
            else if (cbxType.Text.Equals("Set FHR Figure"))
            {
                textBlock1.Text = "Set FHR Figure";
                FHR = int.Parse(setPercent.Text);
                chose_event = 1;

            }
            //可変値
            else if (cbxType.Text.Equals("Set Down Percent"))
            {
                textBlock1.Text = "Set Down Percent";
                Percent = (100.0f - float.Parse(setPercent.Text)) / 100.0f;
                chose_event = 2;

            }
            else
            {
                textBlock1.Text = "Please Chose";
            }

        }
        private void SetFlag_Click(object sender, RoutedEventArgs e)
        {
            if (setFlag.Text.Equals("Ready"))
            {
                flag = 1;
                setFlag.Text = "Task_ready";
            }
            else if (setFlag.Text.Equals("Task_ready"))
            {
                flag = 2;
                setFlag.Text = "Task_speak";
            }
            else if (setFlag.Text.Equals("Task_speak"))
            {
                flag = -3;
                setFlag.Text = "Finish:Please push flag";
            }
            else if (setFlag.Text.Equals("Finish:Please push flag"))
            {
                flag = 0;
                setFlag.Text = "Ready";
            }
            else
            {
                flag = 0;
                setFlag.Text = "Ready";
            }

        }

            private void WindowClosed(object sender, EventArgs e)
        {
            if (sw != null)
            {
                sw.Close();
            }
            if (st != null)
            {
                st.Close();
            }
        }


    }
}
