using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace DownloadJSONFile
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            timer1.Interval = int.Parse(Properties.Settings.Default.Interval) * 1000;
            timer1.Enabled = true;
            button1.Enabled = false;
            button2.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            button1.Enabled = true;
            button2.Enabled = false;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            //create patient and order file directory
            string dateTime = DateTime.Now.ToString("yyyyMMdd");
            string datePath = Properties.Settings.Default.FilePath + dateTime; ;
            Directory.CreateDirectory(datePath);

            string patientPath = datePath + "\\Patients";
            Directory.CreateDirectory(patientPath);

            string orderPath = datePath + "\\Orders";
            Directory.CreateDirectory(orderPath);

            List<Patient> patientList = null;
            List<OrderSource> orderList = null;
            string patientFileText = "";
            string orderFileText = "";

            //Handle Patient
            GetjsonStream(Properties.Settings.Default.SourceAdmissionUrl);
            string tempPatient = Properties.Settings.Default.FilePath + "tempPatient.txt";

            //save patient json file if not exist
            if (File.Exists(tempPatient))
            {
                patientFileText = File.ReadAllText(tempPatient, Encoding.UTF8);
                patientList = DeserializeJsonToList<Patient>(patientFileText);

                if (patientList != null && patientList.Count > 0)
                {
                    foreach (Patient p in patientList)
                    {
                        string patientFile = patientPath + "\\" +  p.CHARTNO + "_" + p.ACCOUNTIDSE + ".txt";

                        if (!File.Exists(patientFile))
                        {
                            try
                            {
                                // send json test to middle, if success, save to file
                                string json = JsonConvert.SerializeObject(p);

                                if (SendAnSMSMessage(json, Properties.Settings.Default.AdmissionUrl))
                                {
                                    System.IO.File.WriteAllText(patientFile, json);
                                }

                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                            }

                        }

                    }
                }
            }

            //Handle Orders
            GetjsonStream(Properties.Settings.Default.SourceOrderAddUrl);
            string tempOrder = Properties.Settings.Default.FilePath + "tempOrder.txt";

            if (File.Exists(tempOrder))
            {
                orderFileText = File.ReadAllText(tempOrder, Encoding.UTF8);
                orderList = DeserializeJsonToList<OrderSource>(orderFileText);

                if (orderList != null && orderList.Count > 0)
                {
                    foreach (OrderSource orderSource in orderList)
                    {
                        string orderFile = orderPath + "\\" + orderSource.CHARTNO 
                            + "_" + orderSource.ACCOUNTIDSE + "_" + orderSource.PHRORDERIDSE + ".txt";

                        if (!File.Exists(orderFile))
                        {
                            try
                            {
                                Order order = new Order();
                                order.Patient = new Patient();

                                order.Patient.CHARTNO = orderSource.CHARTNO;
                                order.Patient.ACCOUNTIDSE = orderSource.ACCOUNTIDSE;

                                order.DrugOrders = new List<OrderSource>();
                                order.DrugOrders.Add(orderSource);

                                string json = JsonConvert.SerializeObject(order);

                                if (SendAnSMSMessage(json, Properties.Settings.Default.OrderAddUrl))
                                {
                                    System.IO.File.WriteAllText(orderFile, json);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                            }


                        }

                    }
                }
            }

        }

        public void GetjsonStream(string sourceUrl)
        {
            //get patient list from WFH and write to tempPatient.txt file
            WebClient client = new WebClient();

            //---------TEST USING MY FILE
            /*
            if (sourceUrl.Equals(Properties.Settings.Default.SourceAdmissionUrl))
                url = "file:///C:/HL7/TempTest.html";
            if (sourceUrl.Equals(Properties.Settings.Default.SourceOrderAddUrl))
                url = "file:///C:/HL7/TempOrder.html";
            */
            //---------------------------
            string url = sourceUrl;
            string content = "";

            try
            {
                byte[] bResult = client.DownloadData(url);
                content = Encoding.UTF8.GetString(bResult);
                Debug.WriteLine("Content: " + content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                string jsonFileName = "";
                switch (sourceUrl.Substring(sourceUrl.LastIndexOf("/") + 1))
                {
                    case "admission":
                        jsonFileName = "tempPatient.txt";
                        break;
                    case "add":
                        jsonFileName = "tempOrder.txt";
                        break;
                    default:
                        jsonFileName = "tempPatient.txt";
                        break;
                }
                System.IO.File.WriteAllText(Properties.Settings.Default.FilePath + jsonFileName, content);
            }
        }

        public static bool SendAnSMSMessage(string json, string apiUrl)
        {
            //write json txt to middleware, return if success
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(apiUrl);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "PUT";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(json);
            }
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var responseText = streamReader.ReadToEnd();
                //Now you have your response.
                //or false depending on information in the response

                return (responseText.ToUpper().IndexOf("TRUE") > 0);
            }
        }

        public static List<T> DeserializeJsonToList<T>(string json) where T : class
        {
            JsonSerializer serializer = new JsonSerializer();
            StringReader sr = new StringReader(json);
            object o = serializer.Deserialize(new JsonTextReader(sr), typeof(List<T>));
            List<T> list = o as List<T>;
            return list;
        }

        public class Patient
        {
            public string PERSONID { get; set; }
            public string HOSPITALCODE { get; set; }
            public string CHARTNO { get; set; }
            public string ACCOUNTIDSE { get; set; }
            public string FAMILYNAME { get; set; }
            public string BIRTHDATE { get; set; }
            public string GENDER { get; set; }
            public string INDATE { get; set; }
        }

        public class OrderSource
        {
            public string CHARTNO { get; set; }
            public string ACCOUNTIDSE { get; set; }
            public string PHRORDERIDSE { get; set; }
            public string SPECIALINSTRUCTION { get; set; }
            public string MEDID { get; set; }
            public double ORDERDOSE { get; set; }
            public string DOSEUNIT { get; set; }
            public string ROUTECODE { get; set; }
            public string REPEATPATTERNCODE { get; set; }
            public string STARTTIME { get; set; }
            public string ENDTIME { get; set; }
        }

        public class Order
        {
            public Patient Patient { get; set; }
            public List<OrderSource> DrugOrders { get; set; }
        }


    }
}
