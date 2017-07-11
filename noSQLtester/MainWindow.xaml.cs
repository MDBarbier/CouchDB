using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace noSQLtester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string databaseServer = "server";
        private static string port = "5984";
        private static string db = "users";
        private static string username = "couchServiceAccount";
        private static string adminUsername = "admin";
        private static string password = "password";
        private static Stopwatch sw = new Stopwatch();

        //Encode the credentials we want to use
        string encodedCredentials = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));


        public MainWindow()
        {
            InitializeComponent();
            CheckConnectionOnWorkerThread();
        }

        private void EnableSubmitChanges()
        {
            submitChangesButton.IsEnabled = true;
        }

        private void DisableSubmitChanges()
        {
            submitChangesButton.IsEnabled = false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var searchQuery = searchQueryTB.Text;
            
            SearchForID(searchQuery);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DisableSubmitChanges();
            var sgQuery = securityGroupTB.Text;

            SearchForSG(sgQuery);
        }

        private void CheckConnectionOnWorkerThread()
        {
            resultTextBlock.Text = "Contacting server... please wait...";
            sw.Start();

            using (BackgroundWorker worker = new BackgroundWorker())
            {
                worker.DoWork += Worker_DoWork;
                worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
                worker.RunWorkerAsync();
            }
        }

        private void SearchForID(string id)
        {
            resultTextBlock.Text = "Contacting server... please wait...";
            sw.Start();

            using (BackgroundWorker worker = new BackgroundWorker())
            {
                worker.DoWork += Worker_DoWork_IDSearch;
                worker.RunWorkerCompleted += Worker_RunWorkerCompleted_IDSearch;
                worker.RunWorkerAsync(searchQueryTB.Text);
            }
        }

        private void Worker_RunWorkerCompleted_IDSearch(object sender, RunWorkerCompletedEventArgs e)
        {
            resultTextBlock.Text = e.Result.ToString();
            TimeKeeping();
            EnableSubmitChanges();
            resultTextBlock.IsReadOnly = false;
        }

        private void TimeKeeping()
        {
            sw.Stop();
            serverResponseTime.Text = sw.Elapsed.ToString();
            sw.Reset();
        }

        private void Worker_DoWork_IDSearch(object sender, DoWorkEventArgs e)
        {
            string query = $"{db}/_design/users/_view/enabled_ad_users?key=\"{e.Argument}\"";

            string url = $"http://{databaseServer}:{port}/{query}";

            //Encode the credentials we want to use
            string encodedCredentials = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(adminUsername + ":" + password));

            WebClient wc = new WebClient();
            wc.Headers.Add("Authorization", "Basic " + encodedCredentials);
            wc.Encoding = Encoding.UTF8;

            try
            {
                byte[] response = wc.DownloadData(new Uri(url));
                e.Result = Encoding.Default.GetString(response);                
            }
            catch (Exception ex)
            {
                e.Result = $"Failed to connect to {databaseServer}:{port} with error: {ex.Message}";
            }
        }

        private void SearchForSG(string id)
        {
            resultTextBlock.Text = "Contacting server... please wait...";
            sw.Start();

            using (BackgroundWorker worker = new BackgroundWorker())
            {
                worker.DoWork += Worker_DoWorkSG;
                worker.RunWorkerCompleted += Worker_RunWorkerCompletedSG; ;
                worker.RunWorkerAsync(securityGroupTB.Text);
            }
        }

        private void Worker_RunWorkerCompletedSG(object sender, RunWorkerCompletedEventArgs e)
        {
            resultTextBlock.Text = e.Result.ToString();
            TimeKeeping();            
        }

        private void Worker_DoWorkSG(object sender, DoWorkEventArgs e)
        {
            string query = $"{db}/_design/users/_view/users_in_specified_sg?key=\"{e.Argument}\"";

            string url = $"http://{databaseServer}:{port}/{query}";

            //Encode the credentials we want to use
            string encodedCredentials = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(adminUsername + ":" + password));

            WebClient wc = new WebClient();
            wc.Headers.Add("Authorization", "Basic " + encodedCredentials);
            wc.Encoding = Encoding.UTF8;

            try
            {
                byte[] response = wc.DownloadData(new Uri(url));
                e.Result = Encoding.Default.GetString(response);                
            }
            catch (Exception ex)
            {
                e.Result = $"Failed to connect to {databaseServer}:{port} with error: {ex.Message}";
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            resultTextBlock.Text = $"{e.Result}";
            TimeKeeping();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            string query = "_all_dbs";

            string url = $"http://{databaseServer}:{port}/{query}";

            //Encode the credentials we want to use
            string encodedCredentials = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(adminUsername + ":" + password));

            WebClient wc = new WebClient();
            wc.Headers.Add("Authorization", "Basic " + encodedCredentials);
            wc.Encoding = Encoding.UTF8;

            try
            {
                byte[] response = wc.DownloadData(new Uri(url));
                e.Result = $"Successfully connected to {databaseServer}:{port} via HTTP Restful API";
            }
            catch (Exception ex)
            {
                e.Result = $"Failed to connect to {databaseServer}:{port} with error: {ex.Message}";
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var yesNoReply = MessageBox.Show("Update JSON?", "Update CouchDB Document", MessageBoxButton.YesNo);

            if (yesNoReply == MessageBoxResult.Yes)
            {
                //get the reply from couchdb
                var JSON = resultTextBlock.Text;

                JObject j = JObject.Parse(JSON);

                //extract the ID
                var id = j["rows"][0]["id"].ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(id))
                {
                    resultTextBlock.Text = "Error: Id missing from JSON";
                    return;
                }

                JObject values = (JObject) j["rows"][0]["value"];
                //string rev = values["_rev"].ToString() ?? string.Empty;
                               

                using (BackgroundWorker worker = new BackgroundWorker())
                {
                    worker.DoWork += Worker_UpdateUser; ;
                    worker.RunWorkerCompleted += Worker_UpdateUserCompleted;
                    worker.RunWorkerAsync(values);
                }
            }
        }

        private void Worker_UpdateUserCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            resultTextBlock.Text = e.Result.ToString();
        }

        private void Worker_UpdateUser(object sender, DoWorkEventArgs e)
        {
            JObject userJSON = (JObject) e.Argument;
            var bytes = Encoding.UTF8.GetBytes(userJSON.ToString());
            string query = $"{userJSON["_id"].ToString()}";

            string url = $"http://{databaseServer}:{port}/{db}/{query}?new_edits=\"False\"";

            //Encode the credentials we want to use
            string encodedCredentials = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));

            WebClient wc = new WebClient();
            wc.Headers.Add("Authorization", "Basic " + encodedCredentials);
            wc.Encoding = Encoding.UTF8;

            try
            {
                byte[] response = wc.UploadData(url, "PUT", bytes);
                e.Result = $"Successfully updated object via HTTP Restful API";
            }
            catch (Exception ex)
            {
                e.Result = $"Failed to update object with error: {ex.Message}";
            }
        }
    }
}
