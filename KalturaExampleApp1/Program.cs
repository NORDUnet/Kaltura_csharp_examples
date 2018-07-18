using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace KalturaExampleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleSetup();
            ProgramConfigurationHandler cfg = new ProgramConfigurationHandler();
            if (!CheckConfiguration(cfg))
            {
                return;
            }
            KalturaExample ex = new KalturaExample(cfg.ApiEndpoint, cfg.PartnerId, cfg.AdminSecret);
            string newEntryId = ex.CreateEntryFromVideoFile();
            ex.ChangeUserIdOnEntry(newEntryId);

        }
        private static void ConsoleSetup()
        {
            Console.Title = "Kaltura example application in C#";
            Console.BackgroundColor = ConsoleColor.DarkMagenta;
            Console.Clear();
            Console.CursorVisible = false;
        }
        private static void ConsoleResetColor()
        {
            Console.BackgroundColor = ConsoleColor.DarkMagenta;
            Console.ForegroundColor = ConsoleColor.White;
        }
        private static bool CheckConfiguration(ProgramConfigurationHandler cfg)
        {
            foreach (string name in cfg.Names)
            {
                if (cfg.UsingDefaults[name])
                {
                    WriteError("Configuration error, please change the placeholder values in the generated XML file.");
                    Console.WriteLine("Press any key to quit.");
                    Console.ReadKey();
                    return false;
                }
            }
            return true;
        }

        private static void WriteError(string notice)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(notice);
            ConsoleResetColor();
        }
    }

    class KalturaExample
    {
        private Kaltura.Configuration Config;
        private Kaltura.Client Client;

        private string KalturaKS;

        /*
         * Class constructor, responsible for configuring/setting up the Kaltura.Configuration and Kaltura.Client.
         */
        public KalturaExample(string apiUrl, string pid, string secret)
        {
            int partnerId = Int32.Parse(pid);
            Config = new Kaltura.Configuration();
            Config.ServiceUrl = apiUrl;
            Client = new Kaltura.Client(Config);
            KalturaKS = Client.GenerateSession(partnerId, secret, "BULK_ADMIN_USER", Kaltura.Enums.SessionType.ADMIN);
            Client.KS = KalturaKS;
        }

        public string CreateEntryFromVideoFile()
        {
            Console.WriteLine("Starting with CreateEntryFromVideoFile()");

            // Creating the entry in Kaltura:
            Kaltura.Types.MediaEntry ent1_data = new Kaltura.Types.MediaEntry();
            ent1_data.Name = "Test entry C#";
            ent1_data.Description = "Video made using the KalturaExampleApp, written in C#";
            ent1_data.UserId = "CSHARP_TEST_USER";
            ent1_data.MediaType = Kaltura.Enums.MediaType.VIDEO;
            Kaltura.Types.MediaEntry ent1 = Kaltura.Services.MediaService.Add(ent1_data).ExecuteAndWaitForResponse(Client);

            Console.WriteLine("Created Kaltura entry: " + ent1.Id);

            Console.WriteLine();
            string dataDir = System.IO.Directory.GetParent(System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).FullName).FullName + "\\datafiles";
            string fileName = "bars_100.avi";
            System.IO.FileStream fileStream = System.IO.File.Open(
                dataDir + "\\" + fileName,
                System.IO.FileMode.Open, System.IO.FileAccess.Read
                );
            Kaltura.Types.UploadToken uploadToken = Kaltura.Services.UploadTokenService.Add().ExecuteAndWaitForResponse(Client);
            Console.WriteLine("Created upload token: " + uploadToken.Id);
            Console.WriteLine("Starting upload of: " + fileName + " to token: " + uploadToken.Id);
            Kaltura.Types.UploadToken ut1 =
                Kaltura.Services.UploadTokenService.Upload(uploadToken.Id, fileStream).ExecuteAndWaitForResponse(Client);
            Console.WriteLine(ut1.Id);
            Console.WriteLine("File uploaded");

            Kaltura.Types.UploadedFileTokenResource uploadedFileToken = new Kaltura.Types.UploadedFileTokenResource();
            uploadedFileToken.Token = uploadToken.Id;
            Console.WriteLine("Addeing uploaded media to entry.");
            Kaltura.Services.MediaService.AddContent(ent1.Id, uploadedFileToken).ExecuteAndWaitForResponse(Client);
            Console.WriteLine("Done with CreateEntryFromVideoFile()");
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
            return ent1.Id;
        }

        public void ChangeUserIdOnEntry(string entryId)
        {
            Console.WriteLine("Starting with ChangeUserIdOnEntry(string entryId)");
            // Fetch the media info for the entry (this is not needed, but used to make the reverse
            // username that we will replace the new one with!)
            Kaltura.Types.MediaEntry mediaEntry = Kaltura.Services.MediaService.Get(entryId).ExecuteAndWaitForResponse(Client);
            Console.WriteLine("Entry " + mediaEntry.Id + " is owned by " + mediaEntry.UserId);
            string newUserId = ReverseString(mediaEntry.UserId);
            Console.WriteLine("Going to update the owner to: " + newUserId);
            Kaltura.Types.MediaEntry mediaEntryUpdateInfo = new Kaltura.Types.MediaEntry();
            mediaEntryUpdateInfo.UserId = newUserId;
            Kaltura.Types.MediaEntry updatedEntry =
                Kaltura.Services.MediaService.Update(entryId, mediaEntryUpdateInfo).ExecuteAndWaitForResponse(Client);
            Console.WriteLine("The updated entry " + updatedEntry.Id + " is now owned by: " + updatedEntry.UserId);
            Console.WriteLine("Done with ChangeUserIdOnEntry(string entryId)");
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }

        /*
         * String containing the KalturaSession hash
         */
        public string GetKalturaKS()
        {
            return KalturaKS;
        }
        private static string ReverseString(string s)
        {
            char[] chars = s.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }
    }

    class ProgramConfigurationHandler
    {
        private Dictionary<string, bool> VariableDefault = new Dictionary<string, bool>();
        private readonly List<string> VariableNames = new List<string> { "AdminSecret", "ApiEndpoint", "PartnerId" };

        public ProgramConfigurationHandler()
        {
            foreach (string name in VariableNames)
            {
                string val = ReadSetting(name);
                if (val == "PLACEHOLDER")
                {
                    VariableDefault.Add(name, true);
                }
                else
                    VariableDefault.Add(name, false);
            }
        }
        ~ProgramConfigurationHandler()
        {
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configFile.Save(ConfigurationSaveMode.Modified);
        }
        private void WriteNotice(string notice)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(notice);
            Console.ResetColor();
        }

        private string ReadSetting(string key)
        {
            var appSettings = ConfigurationManager.AppSettings;
            string result = appSettings[key] ?? "PLACEHOLDER";
            if (result == "PLACEHOLDER")
                WriteSetting(key, result);
            return result;
        }

        private void WriteSetting(string key, string value)
        {
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = configFile.AppSettings.Settings;
            if (settings[key] == null)
                settings.Add(key, value);
            else
                settings[key].Value = value;

            if (value != "PLACEHOLDER")
                VariableDefault[key] = false;

            configFile.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
        }

        public List<string> Names
        {
            get => VariableNames;
        }
        public Dictionary<string, bool> UsingDefaults
        {
            get => VariableDefault;
        }
        public string ApiEndpoint
        {
            get => ReadSetting("ApiEndpoint");
            set => WriteSetting("ApiEndpoint", value);
        }
        public string AdminSecret
        {
            get => ReadSetting("AdminSecret");
            set => WriteSetting("AdminSecret", value);
        }
        public string PartnerId
        {
            get => ReadSetting("PartnerId");
            set => WriteSetting("PartnerId", value);
        }
    }
}
