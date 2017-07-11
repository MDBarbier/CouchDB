using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace noSQL_addViews
{
    class Program
    {
        static void Main(string[] args)
        {
            //Define the view names
            string viewPath = "_design/users";

            //set up URL
            string baseurl = "http://server:5984/";

            //DB name
            string dbname = "users/";

            //Define credentials
            string username = "admin";
            string password = "password";

            //Encode the credentials we want to use
            string encodedCredentials = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));

            JObject enabledFunction = new JObject();
            enabledFunction.Add("map", "function(doc) { if (doc.status == 'Enabled') {emit(doc._id, doc);}}");

            JObject sgMatchFunction = new JObject();
            sgMatchFunction.Add("map", "function(doc) { if (doc.status == 'Enabled') { for ( var i in doc.sgmembership) {emit(i, doc);}}}");

            JObject views = new JObject();

            views.Add(new JProperty("enabled_ad_users", enabledFunction));
            views.Add(new JProperty("users_in_specified_sg", sgMatchFunction));

            JObject jo = new JObject(
                    new JProperty("id", viewPath),
                    new JProperty("language", "javascript"),
                    new JProperty("views", views)
                    );

            var bytes = Encoding.UTF8.GetBytes(jo.ToString());

            WebClient wc = new WebClient();
            wc.Headers.Add("Authorization", "Basic " + encodedCredentials);
            wc.Encoding = Encoding.UTF8;

            try
            {
                string target = baseurl + dbname + viewPath;
                byte[] response = wc.UploadData(target, "PUT", bytes);

                //handle success
                Console.WriteLine(DateTime.Now + " - Document added: " + baseurl + dbname + viewPath);

                using (var writer = System.IO.File.AppendText(@"c:\Logfiles\nosql-log.txt"))
                {
                    writer.WriteLine(DateTime.Now + " - Document added: " + baseurl + dbname + viewPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now + " - ERROR - Document add failed: " + baseurl + dbname + viewPath + ", " + ex.Message);

                using (var writer = System.IO.File.AppendText(@"c:\Logfiles\nosql-log.txt"))
                {
                    writer.WriteLine(DateTime.Now + " - ERROR - Document add failed: " + baseurl + dbname + viewPath + ", " + ex.Message);
                }
            }
        }
    }
}
