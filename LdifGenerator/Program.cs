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
        public int MaxLineNumber { get; } = 10;

        static int Main(string[] args)
        {
#if DEBUG
            args = new string[]
            {
                "-b=dc=example,dc=com",
                "-o=C:/Temp/generated",
                "-s=1000",
                "-m=1000"
            };
#endif
            return CommandLineApplication.Execute<Program>(args);

        }

        private void OnExecute(CommandLineApplication app)
        {
            try
            {
                string path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + @"\Data\";

                string pgfhath = AppDomain.CurrentDomain.BaseDirectory + @"\Data\";
                string[] OUNames = File.ReadAllLines(path + "organizational-units.txt");
                string[] fNames = File.ReadAllLines(path + "family-names.txt");
                string[] gNames = File.ReadAllLines(path + "given-names.txt");
                string[] personClasses = File.ReadAllLines(path + "person-classes.txt");
                string[] mailhosts = File.ReadAllLines(path + "mail-hosts.txt");
                string[] positions = File.ReadAllLines(path + "positions.txt");
                string[] ranks = File.ReadAllLines(path + "title-ranks.txt");

                List<string> DNs = new List<string>();
                Random random = new Random(2);
                using (var writer = new StreamWriter(new FileStream(OutputFile + ".ldif", FileMode.Create)))
                {
                    foreach(string entry in GenerateOU(BaseDN, OUNames))
                    {
                        writer.WriteLine(entry+"\r");
                    }

                    for (int i = 0; i < Size; i += 1)
                    {
                        string ou = OUNames[random.Next(0,OUNames.Length)];
                        string name = i + " " + fNames[random.Next(0, fNames.Length)] +" "+ gNames[random.Next(0, gNames.Length)];
                        string dn = "cn=" + name + ",ou=" + ou + "," + BaseDN;
                        DNs.Add(dn);
                        
                        string host = mailhosts[random.Next(0, mailhosts.Length)];
                        writer.WriteLine(GeneratePerson(dn, name, personClasses, ou,host) + "\r");
                    }
                }
                using (var writer = new StreamWriter(new FileStream(OutputFile + "_mod.ldif", FileMode.Create)))
                {
                    foreach(string dn in DNs)
                    {
                        string title = ranks[random.Next(0, ranks.Length)] + " " + positions[random.Next(0, positions.Length)];
                        writer.WriteLine(ModifyPerson(dn,title) + "\r");
                    }
                }

                using (var writer = new StreamWriter(new FileStream(OutputFile + "_del.ldif", FileMode.Create)))
                {
                    foreach (string dn in DNs)
                    {
                        writer.WriteLine(DeletePerson(dn) + "\r");
                    }
                    foreach (string entry in DeleteOU(BaseDN, OUNames))
                    {
                        writer.WriteLine(entry + "\r");
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
            builder.AppendLine("add: title");
            builder.AppendLine("title: " + title);

            return builder.ToString();
        }
        private string GeneratePerson(string dn, string name, string[] classes, string ou, string host)
        {
            var rand = new Random();
            var builder = new StringBuilder();
            builder.AppendLine("dn: " + dn);
            builder.AppendLine("cn: " + name);
            builder.AppendLine("sn: " + name);
            var pClass = classes[rand.Next(1, classes.Length)];

            builder.AppendLine("objectclass: "+ pClass);
            if (pClass.Equals("organizationalPerson"))
            {
                builder.AppendLine("ou: " + ou);
            }
            if (pClass.Equals("inetOrgPerson"))
            {
                builder.AppendLine("ou: " + ou);
                builder.AppendLine(("mail: " + name.Replace(' ', '.') + "@" + ou.Replace(" ", String.Empty) + "." + host).ToLowerInvariant());
            }
            return builder.ToString();
        }

        private string DeletePerson(string dn)
        {
            var builder = new StringBuilder();
            builder.AppendLine("dn: "+ dn);
            builder.AppendLine("changetype: delete");
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
                builder.AppendLine("dn: ou=" + name + "," + baseDN);
                builder.AppendLine("changetype: delete");
                myList.Add(builder.ToString());
            }
            return myList;
        }
    }
}
