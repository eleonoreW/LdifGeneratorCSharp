using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;

namespace LdifGenerator
{
    class Program
    {
        [Required]
        [Option("-o|--outputFile <File>", "Specifies the output ldif file.", CommandOptionType.SingleValue)]
        public string OutputFile { get; }

        [Required]
        [Option("-b|--baseDN <File>", "Specifies the base DN in which the entries should be created.", CommandOptionType.SingleValue)]
        public string BaseDN { get; }

        [Required]
        [Option("-s|--size <SizeLimit>", "Specifies how many entries should be generated.", CommandOptionType.SingleValue)]
        public int Size { get; } = 10;

        [Option("--seed <Seed>", "Specifies the Seed for the random number generator.", CommandOptionType.SingleValue)]
        public int Seed { get; } = -1;

        [Option("-m|--maxLineNumber <LinesNumber>", "Specifies how much entries must be  written in asingle file. If need be, multiple files will be created.", CommandOptionType.SingleValue)]
        public int MaxFileSize { get; set; } = -1;

        [Option("-p|--personIds <PersonId>", "If used, the entries CN and DN must contain a number. Example: cn=200 Hollandsworth Carlos,dc=example,dc=com", CommandOptionType.NoValue)]
        public bool PersonNumbers { get; set; } = false;

        [Option("-w|--UseWindowsEOL ", "If used, the Windows EOL will be used.", CommandOptionType.NoValue)]
        public bool UseWindowsEOL { get; set; } = false;

        string EOL;

        string[] OUNames;
        string[] fNames;
        string[] gNames;
        string[] personClasses;
        string[] mailhosts;
        string[] positions;
        string[] ranks;
        string[] localities;
        Dictionary<string, (string DN, string ou, string objectclass)> personDictionary;


        static int Main(string[] args)
        {
#if DEBUG
            args = new string[]
            {
                "-b=dc=example,dc=com",
                "-o=C:/Temp/generated_5000",
                "-s=200",
                //"-p",
                "--seed=2",
                //"-m=200"
            };
#endif
            return CommandLineApplication.Execute<Program>(args);
        }

        private void OnExecute(CommandLineApplication app)
        {
            try
            {
                EOL = UseWindowsEOL ? "\r\n" : "\n";

                string path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + @"\Data\";

                OUNames = File.ReadAllLines(path + "organizational-units.txt");
                fNames = File.ReadAllLines(path + "family-names.txt");
                gNames = File.ReadAllLines(path + "given-names.txt");
                personClasses = File.ReadAllLines(path + "person-classes.txt");
                mailhosts = File.ReadAllLines(path + "mail-hosts.txt");
                positions = File.ReadAllLines(path + "positions.txt");
                ranks = File.ReadAllLines(path + "title-ranks.txt");
                localities = File.ReadAllLines(path + "localities.txt");

                personDictionary = new Dictionary<string, (string DN, string ou, string objectclass)>();
                Random random = Seed != -1 ? new Random(Seed) : new Random();

                double nbFile = 1;
                if (MaxFileSize != -1)
                {
                    nbFile = Math.Ceiling((float)Size / (float)MaxFileSize);
                }
                else
                {
                    MaxFileSize = Size;
                }
                int dup = 0;
                while (personDictionary.Count < Size)
                {
                    string ou = GetRandFromArray(OUNames, random);
                    string name = GetRandFromArray(fNames, random) + " " + GetRandFromArray(gNames, random);
                    if (PersonNumbers)
                    {
                        name = personDictionary.Count + " " + name;
                    }
                    string dn = "cn=" + name + ",ou=" + ou + "," + BaseDN;
                    string objectClass = GetRandFromArray(personClasses, random);
                    if (!personDictionary.TryAdd(name, (dn, ou, objectClass)))
                    {
                        dup++;
                    }
                }

                Console.WriteLine("{0} Duplicate", dup);

                var enumerator = personDictionary.GetEnumerator();
                enumerator.MoveNext();
                for (int j = 0; j < nbFile; j += 1)
                {
                    using var writer = new StreamWriter(new FileStream(OutputFile + "_" + j + ".ldif", FileMode.Create))
                    {
                        NewLine = EOL
                    };
                    int max = (j == nbFile - 1 && MaxFileSize != Size && (Size % MaxFileSize) != 0) ? Size % MaxFileSize : MaxFileSize;
                    for (int i = 1; i <= max; i += 1)
                    {
                        var person = enumerator.Current;
                        string host = GetRandFromArray(mailhosts, random);
                        writer.WriteLine(GeneratePerson(person.Value.DN, person.Key, person.Value.objectclass, person.Value.ou, host, random));
                        enumerator.MoveNext();
                    }
                }

                using (var writer = new StreamWriter(new FileStream(OutputFile + "_ou.ldif", FileMode.Create)))
                {
                    writer.NewLine = EOL;
                    foreach (string entry in GenerateOU(BaseDN, OUNames))
                    {
                        writer.WriteLine(entry);
                    }
                }

                using (var writer = new StreamWriter(new FileStream(OutputFile + "_mod_inetOrgPerson.ldif", FileMode.Create)))
                {
                    writer.NewLine = EOL;
                    foreach (var person in personDictionary)
                    {
                        if (!string.Equals(person.Value.objectclass, "inetOrgPerson"))
                        {
                            continue;
                        }

                        string title = GetRandFromArray(ranks, random) + " " + GetRandFromArray(positions, random);
                        writer.WriteLine(ModifyPersonAttribute(person.Value.DN, "title", "add", title));
                    }
                }

                using (var writer = new StreamWriter(new FileStream(OutputFile + "_mod_all.ldif", FileMode.Create)))
                {
                    writer.NewLine = EOL;
                    foreach (var person in personDictionary)
                    {
                        string description = GetRandFromArray(ranks, random) + " " + GetRandFromArray(positions, random);
                        writer.WriteLine(ModifyPersonAttribute(person.Value.DN, "description", "replace", description));
                    }
                }

                using (var writer = new StreamWriter(new FileStream(OutputFile + "_del.ldif", FileMode.Create)))
                {
                    writer.NewLine = EOL;
                    foreach (var person in personDictionary)
                    {
                        writer.WriteLine(DeletePerson(person.Value.DN));
                    }
                    foreach (string entry in DeleteOU(BaseDN, OUNames))
                    {
                        writer.WriteLine(entry);
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

        private string ModifyPersonAttribute(string dn, string attribute, string action, string value)
        {
            var builder = new StringBuilder();
            builder.Append("dn: " + dn + EOL);
            builder.Append("changetype: modify" + EOL);
            builder.Append(action + ": " + attribute + EOL);
            builder.Append(attribute + ": " + value + EOL);

            return builder.ToString();
        }

        private string GetRandFromArray(string[] myArray, Random rand)
        {
            return myArray[rand.Next(0, myArray.Length)];
        }

        private string GeneratePerson(string dn, string name, string pClass, string ou, string host, Random random)
        {
            var builder = new StringBuilder();
            builder.Append("dn: " + dn + EOL);
            builder.Append("changetype: add" + EOL);
            builder.Append("cn: " + name + EOL);
            builder.Append("sn: " + name + EOL);
            builder.Append("description: This is " + name + "'s description." + EOL);
            if (!pClass.Equals("person"))
            {
                builder.Append("ou: " + ou + EOL);
                builder.Append("l: " + GetRandFromArray(localities, random) + EOL);
                builder.Append("telephoneNumber: +33" + random.Next(100000000) + EOL);
            }
            if (pClass.Equals("inetOrgPerson"))
            {
                builder.Append(("mail: " + name.Replace(' ', '.') + "@" + ou.Replace(" ", String.Empty) + "." + host).ToLowerInvariant() + EOL);
                if (personDictionary.Any())
                {
                    builder.Append("secretary: " + personDictionary.ElementAt(random.Next(personDictionary.Count)).Value.DN + EOL);
                    builder.Append("manager: " + personDictionary.ElementAt(random.Next(personDictionary.Count)).Value.DN + EOL);
                }
            }
            builder.Append("objectclass: " + pClass + EOL);
            return builder.ToString();
        }

        private string DeletePerson(string dn)
        {
            var builder = new StringBuilder();
            builder.Append(dn + EOL);
            return builder.ToString();
        }

        private List<string> GenerateOU(string baseDN, string[] ouNames)
        {
            List<string> myList = new List<string>();
            foreach (string name in ouNames)
            {
                var builder = new StringBuilder();
                builder.Append("dn: ou=" + name + "," + baseDN + EOL);
                builder.Append("objectclass: organizationalUnit" + EOL);
                builder.Append("ou: " + name + EOL);
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
                builder.Append("ou=" + name + "," + baseDN + EOL);
                myList.Add(builder.ToString());
            }
            return myList;
        }
    }
}
