using MVVMBase.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace noSQL
{
    class Program
    {
        static void Main(string[] args)
        {
            List<UOWUser> users = new List<UOWUser>(); //list to hold actual data we want to put in db

            string testID = "";

            //get AD data
            var adData = ReadFromAD(testID);            

            //Mash up data into our list to add to DB
            users = MashDataTogether(adData);

            //Add the data to DB
            AddUsersToCouchDB(users);

            Console.WriteLine("Execution finished.");
            Console.ReadLine();
        }

        private static List<UOWUser> MashDataTogether(List<XElement> adData)
        {
            List<UOWUser> tempusers = new List<UOWUser>(); //list to temporarily hold actual data we want to put in db

            Parallel.ForEach(adData, user => {
                
                    string faculty = string.Empty;
                    DateTime graduationDate = new DateTime();

                if (user.Attribute("id") != null)
                {
                    //parse ID
                    int id = 0;
                    bool idresult = int.TryParse(user.Attribute("id").Value, out id);

                    if (!idresult)
                    {
                        return; // cannot add a user who doesn't have ID number
                    }

                    //get security groups
                    List<string> memberof = new List<string>();
                    foreach (var item in user.Element("memberof").Elements())
                    {
                        memberof.Add(item.Value);
                    }

                    //get active status
                    bool activeStatus = (!user.Element("account-disabled").Value.Equals("true")) ? true : false;

                    if (user.Element("kac-usercategory").Value.ToLower().Equals("student"))
                    {
                        var sitsuser = new Uri("http://data.com/users/" + id + "/full").Download().Root.Element("user");

                        //gets sits data if there was a match
                        if (sitsuser != null)
                        {
                            //get expected graduation date
                            graduationDate = new DateTime(); //initialise zeroed
                            bool dateparseresult = DateTime.TryParse(sitsuser.Element("calc-eed").Value ?? string.Empty, out graduationDate); //if they have a date in SITS, use that

                            //go through all the sces and find current one and get faculty from it
                            faculty = getFaculty(sitsuser);

                        }
                    }

                    //create user object
                    UOWUser tempUser = new UOWUser(id, user.Element("samaccountname").Value, faculty, memberof, activeStatus, graduationDate);

                    lock (tempusers)
                    {
                        tempusers.Add(tempUser);
                    }
                }
            });
            

            return tempusers;
        }

        private static string getFaculty(XElement sitsuser)
        {
            var scj = sitsuser.Element("scj") ?? null;

            if (scj == null)
                return "No SCJ found";

            var sces = scj.Elements("sce").ToList() ?? null;
                        
            if (scj == null || (sces == null || sces.Count <1))
            {
                return "No SCJ/SCE found";
            }

            foreach (var sce in sces)
            {
                if (sce.Element("academicyear").Value.Equals(getCurrentAcademicYear()))
                {
                    return sce.Element("faculty").Value ?? "No faculty registered in SITS";                    
                }
            }

            return "No current SCE found";
        }

        private static object getCurrentAcademicYear()
        {
            DateTime now = DateTime.Now;
            string academicYear = "";

            if (now.Month >= 9)
            {
                academicYear = $"{now.Year.ToString().Substring(2, 2)}/{now.AddYears(1).Year.ToString().Substring(2, 2)}";
            }
            else
            {
                academicYear = $"{now.AddYears(-1).Year.ToString().Substring(2,2)}/{now.Year.ToString().Substring(2,2)}";
            }

            return academicYear;
        }

        private static void AddUsersToCouchDB(List<UOWUser> users)
        {
            //Define the commands we want to use
            string command = "users/";

            //Define credentials
            string username = "couchServiceAccount";
            string password = "password";

            //Encode the credentials we want to use
            string encodedCredentials = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));

            //set up URL
            string baseurl = "http://server:5984/";

            //construct the json
            foreach (var user in users)
            {
                //add the user ID onto the command string to create a Document with their ID as the name
                command = @"uow_users/";
                command += user.Id;

                //create JObject for security groups
                JObject tempSGJSON = new JObject();
                foreach (var sg in user.SgMembership)
                {

                    var itemname = sg.Split(',')[0].Substring(3);
                    tempSGJSON.Add(itemname, sg);
                }

                JObject jo = new JObject(
                    new JProperty("id", user.Id.ToString()),
                    new JProperty("username", user.Username),
                    new JProperty("expectedenddate", user.ExpectedGraduation),
                    new JProperty("faculty", user.Faculty),
                    new JProperty("status", user.Active == true ? "Enabled" : "Disabled"),
                    new JProperty("sgmembership", tempSGJSON)
                    );

                var bytes = Encoding.UTF8.GetBytes(jo.ToString());

                WebClient wc = new WebClient();
                wc.Headers.Add("Authorization", "Basic " + encodedCredentials);
                wc.Encoding = Encoding.UTF8;
                
                try
                {
                    byte[] response = wc.UploadData(baseurl + command, "PUT", bytes);
                    
                    //handle success
                    Console.WriteLine(DateTime.Now + " - Document added: " + baseurl + command);

                    using (var writer = System.IO.File.AppendText(@"c:\Logfiles\nosql-log.txt"))
                    {
                        writer.WriteLine(DateTime.Now + " - Document added: " + baseurl + command);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(DateTime.Now + " - ERROR - Document add failed: " + baseurl + command + ", " + ex.Message);

                    using (var writer = System.IO.File.AppendText(@"c:\Logfiles\nosql-log.txt"))
                    {
                        writer.WriteLine(DateTime.Now + " - ERROR - Document add failed: " + baseurl + command + ", " + ex.Message);
                    }
                }
            }            
        }

        private static List<XElement> ReadFromAD(string id = "")
        {
            //get AD data
            string url = "http://data.winchester.ac.uk/ad/users/?kac-usercategory=";

            if (!string.IsNullOrEmpty(id))
            {
                url = $"http://data.winchester.ac.uk/ad/users/{id}?kac-usercategory=";
            }

            //get staff
            var userdata = new Uri(url + "staff").Download().Root.Elements("user").ToList();

            //get students
            userdata.AddRange(new Uri(url + "student").Download().Root.Elements("user").ToList());

            //for testing
            //userdata.AddRange(new Uri(url).Download().Root.Elements("user").ToList());

            return userdata; 
        }

        private static List<XElement> ReadFromSITS(string id = "")
        {
            //get SITS data for current students
            string url = "http://data.winchester.ac.uk/sits/users/current";

            if (!string.IsNullOrEmpty(id))
            {
                url = $"http://data.winchester.ac.uk/sits/users/{id}";
            }

            //get students
            var userdata = new Uri(url).Download().Root.Elements("user").ToList();

            return userdata;
        }
    }

    public class UOWUser
    {
        public int Id { get; set; } //from AD
        public string Username { get; set; } //from AD
        public List<string> SgMembership { get; set; } //from AD
        public string Faculty { get; set; } //from SITS
        public bool Active { get; set; } //from AD
        public DateTime ExpectedGraduation { get; set; } //from SITS

        public UOWUser() { }

        public UOWUser(int id, string username, string faculty, List<string> sgs, bool active, DateTime graduation)
        {
            this.Id = id;
            this.Username = username;
            this.Faculty = faculty;
            this.SgMembership = sgs;
            this.Active = active;
            this.ExpectedGraduation = graduation;
        }
    }
}
