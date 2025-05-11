using Bring.Sharepoint;
using Bring.Sqlserver;
using Bring.XmlConfig;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Bring.SPODataQuality
{
    internal class RefreshSPOLists
    {
        private static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("DEBUG: Iniciando Main");
                Console.WriteLine("CURRENT TIME: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                var (username, password) = ConfigurationReader.GetSharePointCredentials();
                SPOUser spoUser = new SPOUser(username, password);
                Console.WriteLine("DEBUG: SPOUser criado");

                var list1 = new SPOList();
                list1.SPOUser = spoUser;
                Console.WriteLine("DEBUG: Primeiro SPOList configurado");

                var list2 = new SPOList();
                list2.SPOUser = spoUser;
                Console.WriteLine("DEBUG: Segundo SPOList configurado");
                if ((uint)args.Length > 0U)
                {
                    string lower = args[0].ToLower();
                    Console.WriteLine("DEBUG: Argumento recebido - " + lower);

                    if (!(lower == "daily"))
                    {
                        if (lower == "monthly")
                        {
                            Console.WriteLine("DEBUG: Executando monthly");
                            RefreshSQLLists.SPOtoSQLUpdate(false);
                        }
                        else
                        {
                            Console.WriteLine("Unrecognized argument, please use daily or monthly as the argument");
                        }
                    }
                    else
                    {
                        Console.WriteLine("DEBUG: Executando daily");
                        RefreshSQLLists.SPOtoSQLUpdate(true);
                    }
                }

                Console.WriteLine("End of requests.");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        public static void GetAllLists()
        {
            Console.WriteLine("DEBUG: Entrando em GetAllLists");
            var (username, password) = ConfigurationReader.GetSharePointCredentials();
            SPOUser spoUser = new SPOUser(username, password);
            Context context = new Context()
            {
                Site = "seed",
                SPOUser = spoUser
            };
            foreach (List allList in (ClientObjectCollection<List>)context.GetAllLists())
            {
                Console.WriteLine("DEBUG: Carregando lista - " + allList.Title);
                context.Ctx.Load<List>(allList, new Expression<Func<List, object>>[1]
                {
                    (Expression<Func<List, object>>) (l => (object) l.IsSystemList)
                });
                context.Ctx.ExecuteQuery();
                Console.WriteLine("List Name: " + allList.Title + "; is: " + allList.IsSystemList.ToString());
            }
        }

        private static void SPODebug(string listName, string ctxURL, SPOUser user)
        {
            Console.WriteLine("DEBUG: Entrando em SPODebug");
            SPOList spoList = new SPOList
            {
                Name = listName,
                Site = ctxURL,
                SPOUser = user,
                CAMLQuery = "<View><RowLimit>1</RowLimit></View>"
            };

            Console.WriteLine("DEBUG: Executando Build");
            spoList.Build();

            Console.WriteLine("DEBUG: Executando PropsToString");
            spoList.PropsToString(spoList.ItemCollection[0]);
        }

        private static void RefreshListsSPO(SPOList sourceList, SPOList destList)
        {
            try
            {
                Console.WriteLine("DEBUG: Iniciando RefreshListsSPO");

                sourceList.Build();
                Console.WriteLine("DEBUG: sourceList.Build concluído");

                destList.Build();
                Console.WriteLine("DEBUG: destList.Build concluído");

                int num1 = 0;
                int num2 = 0;

                string[,] actualFields = GetActualFields(sourceList, destList);
                Console.WriteLine("DEBUG: Campos obtidos");

                if ((uint)sourceList.ItemCollection.Count > 0U)
                    num1 = (int)sourceList.ItemCollection[sourceList.ItemCollection.Count - 1]["ID"];
                if ((uint)destList.ItemCollection.Count > 0U)
                    num2 = (int)destList.ItemCollection[destList.ItemCollection.Count - 1]["ID"];

                if (num2 < num1)
                {
                    Console.WriteLine("DEBUG: Adicionando novos itens");
                    do
                    {
                        destList.AddItem();
                        ++num2;
                    }
                    while (num2 < num1);

                    Console.WriteLine("Adding new items...");
                    destList.Update();
                    Console.WriteLine("Done adding items.");
                }

                for (int index1 = 0; index1 < sourceList.ItemCollection.Count; ++index1)
                {
                    int id = (int)sourceList.ItemCollection[index1]["ID"];
                    for (int index2 = 0; index2 < actualFields.Length / 2; ++index2)
                        destList.ItemCollection.GetById(id)[actualFields[index2, 0]] = sourceList.ItemCollection[index1][actualFields[index2, 1]];
                    destList.ItemCollection.GetById(id).Update();
                }

                destList.Ctx.ExecuteQuery();
                Console.WriteLine(sourceList.Site + " " + sourceList.Name + " -> " + destList.Site + " " + destList.Name + ": Done!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR in RefreshListsSPO: " + ex.Message);
            }
        }

        private static string[,] GetActualFields(SPOList listone, SPOList listtwo)
        {
            Console.WriteLine("DEBUG: Entrando em GetActualFields");

            List<Field> fields1 = GetFields(listone);
            List<Field> fields2 = GetFields(listtwo);

            string[,] strArray = new string[fields1.Count, 2];
            int index1 = 0;
            int index2 = 0;

            foreach (Field field1 in fields1)
            {
                Field field2;
                do
                {
                    field2 = fields2[index2];
                    if (field1.Title == field2.Title)
                    {
                        strArray[index1, 0] = field2.InternalName;
                        strArray[index1, 1] = field1.InternalName;
                        Console.WriteLine($"DEBUG: Match found - {field1.Title}");
                    }
                    ++index2;
                }
                while (field1.Title != field2.Title && index2 < fields2.Count);

                ++index1;
                index2 = 0;
            }

            return strArray;
        }

        private static List<Field> GetFields(SPOList list)
        {
            Console.WriteLine("DEBUG: Entrando em GetFields");

            List<Field> fieldList = new List<Field>();
            foreach (Field field in (ClientObjectCollection<Field>)list.Fields)
            {
                if (!field.FromBaseType || field.InternalName == "Title")
                {
                    fieldList.Add(field);
                    Console.WriteLine($"DEBUG: Campo adicionado - {field.Title}");
                }
            }

            return fieldList;
        }
    }
}