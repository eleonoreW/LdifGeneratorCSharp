using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;
using McMaster.Extensions.CommandLineUtils;

namespace LdifGenerator
{
    class Program
    {
        [Required]
        [Option("-o|--outputFile <File>", "Specifies the output ldif file", CommandOptionType.SingleValue)]
        public string OutputFile { get; }

        [Required]
        [Option("-b|--baseDN <File>", "Specifies the entry csv file", CommandOptionType.SingleValue)]
        public string BaseDN { get; }

        [Required]
        [Option("-s|--size <SizeLimit>", "Specifies the Search size timeout.", CommandOptionType.SingleValue)]
        public int Size { get; } = 10;

        [Option("-m|--maxLineNumber <linesNumber>", "Specifies the Search size timeout.", CommandOptionType.SingleValue)]
        public int MaxFileSize { get; set; } = -1;

        [Option("-l|--EOL ", "Specifies if we must use Windows EOL.", CommandOptionType.SingleValue)]
        public bool EOL { get; set; } = false;

        public string eol { get; set; } = "\r";

        static int Main(string[] args)
        {
#if DEBUG
            args = new string[]
            {
                "-b=dc=example,dc=com",
                "-o=C:/Temp/generated",
                "-s=1000",
                //"-m=300"
            };
#endif
            return CommandLineApplication.Execute<Program>(args);
        }

        private void OnExecute(CommandLineApplication app)
        {
            try
            {
                string path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + @"\Data\";

                string[] OUNames = File.ReadAllLines(path + "organizational-units.txt");
                string[] fNames = File.ReadAllLines(path + "family-names.txt");
                string[] gNames = File.ReadAllLines(path + "given-names.txt");
                string[] personClasses = File.ReadAllLines(path + "person-classes.txt");
                string[] mailhosts = File.ReadAllLines(path + "mail-hosts.txt");
                string[] positions = File.ReadAllLines(path + "positions.txt");
                string[] ranks = File.ReadAllLines(path + "title-ranks.txt");

                List<string> DNs = new List<string>();
                Random random = new Random(2);

                double nbFile=1;
                if (MaxFileSize != -1)
                {
                    nbFile = Math.Ceiling((float)Size / (float)MaxFileSize);
                }
                else
                {
                    MaxFileSize = Size;
                }

                for (int j=0; j < nbFile; j = j+1)
                {
                    using (var writer = new StreamWriter(new FileStream(OutputFile + "_"+j+".ldif", FileMode.Create)))
                    {
                        int max = (j == nbFile-1 && MaxFileSize != Size) ? Size % MaxFileSize : MaxFileSize;
                        for (int i = 1; i <= max; i += 1)
                        {
                            var id = i + MaxFileSize * j;
                            string ou = OUNames[random.Next(0, OUNames.Length)];
                            string name = id + " " + fNames[random.Next(0, fNames.Length)] + " " + gNames[random.Next(0, gNames.Length)];
                            string dn = "cn=" + name + ",ou=" + ou + "," + BaseDN;
                            DNs.Add(dn);

                            string host = mailhosts[random.Next(0, mailhosts.Length)];
                            writer.Write(GeneratePerson(dn, name, personClasses, ou, host) + "\r");
                        }
                    }
                }

                using (var writer = new StreamWriter(new FileStream(OutputFile + "_ou.ldif", FileMode.Create)))
                {
                    foreach(string entry in GenerateOU(BaseDN, OUNames))
                    {
                        writer.Write(entry + "\r");
                    }

                    
                }

                using (var writer = new StreamWriter(new FileStream(OutputFile + "_mod.ldif", FileMode.Create)))
                {
                    foreach(string dn in DNs)
                    {
                        string title = ranks[random.Next(0, ranks.Length)] + " " + positions[random.Next(0, positions.Length)];
                        writer.Write(ModifyPerson(dn,title)+"\r");
                    }
                }

                using (var writer = new StreamWriter(new FileStream(OutputFile + "_del.ldif", FileMode.Create)))
                {
                    foreach (string dn in DNs)
                    {
                        writer.Write(DeletePerson(dn) + "\r");
                    }
                    foreach (string entry in DeleteOU(BaseDN, OUNames))
                    {
                        writer.Write(entry + "\r");
                    }
                }
            }
            catch (Exception e)
            {
                while (e != null)
                {
                    Console.WriteLine(e.GetType().Name + " " + e.Message);
                    Console.WriteLine(e.StackTrace);
                    e = e.InnerException;
                }

                throw;
            }
        }

        private string ModifyPerson(string dn, string title)
        {
            var builder = new StringBuilder();
            builder.AppendLine("dn: " + dn);
            builder.AppendLine("changetype: modify");
            builder.AppendLine("add: description");
            builder.AppendLine("description: " + title);

            return builder.ToString();
        }
        private string GeneratePerson(string dn, string name, string[] classes, string ou, string host)
        {
            var rand = new Random();
            var builder = new StringBuilder();
            builder.AppendLine("dn: " + dn);
            builder.AppendLine("changetype: add");
            builder.AppendLine("cn: " + name);
            builder.AppendLine("sn: " + name);
            var pClass = classes[rand.Next(1, classes.Length)];
            if (pClass.Equals("organizationalPerson"))
            {
                builder.AppendLine("ou: " + ou);
            }
            if (pClass.Equals("inetOrgPerson"))
            {
                builder.AppendLine("ou: " + ou);
                builder.AppendLine(("mail: " + name.Replace(' ', '.') + "@" + ou.Replace(" ", String.Empty) + "." + host).ToLowerInvariant());
            }
            builder.AppendLine("objectclass: " + pClass);
            return builder.ToString();
        }

        private string DeletePerson(string dn)
        {
            var builder = new StringBuilder();
            builder.AppendLine(dn);
            return builder.ToString();
        }

       

        private List<string> GenerateOU(string baseDN, string[] ouNames)
        {
            List<string> myList = new List<string>();
            foreach(string name in ouNames)
            {
                var builder = new StringBuilder();
                builder.AppendLine("dn: ou=" + name + "," + baseDN);
                builder.AppendLine("objectclass: organizationalUnit");
                builder.AppendLine("ou: " + name);
                myList.Add(builder.ToString());
            }
            return myList;
        }
        private List<string> DeleteOU(string baseDN, string[] ouNames)
        {
            List<string> myList = new List<string>();
            foreach (string name in ouNames)
            {
                var builder = new StringBuilder();
                builder.AppendLine("ou=" + name + "," + baseDN);
                myList.Add(builder.ToString());
            }
            return myList;
        }
    }
}
