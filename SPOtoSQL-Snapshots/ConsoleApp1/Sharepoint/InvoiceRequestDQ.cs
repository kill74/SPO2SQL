// Decompiled with JetBrains decompiler
// Type: Bring.Sharepoint.InvoiceRequestDQ
// Assembly: ConsoleApp1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 2529ACA9-9F81-4C49-8E47-E8B02D261367
// Assembly location: C:\Users\KEVIN\Desktop\Visual Studio\SPtoSP\ConsoleApp1.exe

using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;

namespace Bring.Sharepoint
{
  internal class InvoiceRequestDQ
  {
    public SPOUser Me { get; set; }

    public bool UpdateApprovers()
    {
      DateTime dateTime = DateTime.Today.AddDays(-3.0);
      string str = "<View><Query><Where><Geq><FieldRef Name='Modified' /><Value Type='DateTime'>" + (dateTime.Year.ToString() + "-" + this.PadStr(dateTime.Month) + "-" + this.PadStr(dateTime.Day) + "T00:00:00Z") + "</Value></Geq></Where></Query></View>";
      SPOList spoList1 = new SPOList();
      spoList1.SPOUser = this.Me;
      spoList1.Name = "Unit";
      spoList1.Site = "seed";
      spoList1.CAMLQuery = str;
      SPOList spoList2 = spoList1;
      Dictionary<string, ListItem> unitDictionary = new Dictionary<string, ListItem>();
      spoList2.Build();
      if ((uint) spoList2.ItemCollection.Count > 0U)
      {
        for (int index = 0; index < spoList2.ItemCollection.Count; ++index)
          unitDictionary.Add((string) spoList2.ItemCollection[index]["Project_x0020_ID"], spoList2.ItemCollection[index]);
        SPOList spoList3 = new SPOList();
        spoList3.SPOUser = this.Me;
        spoList3.Name = "invoice request";
        spoList3.Site = "selfservice/invoicerequest";
        spoList3.CAMLQuery = this.QueryBuilder(unitDictionary);
        SPOList spoList4 = spoList3;
        int num = 0;
        spoList4.Build();
        foreach (ListItem listItem1 in (ClientObjectCollection<ListItem>) spoList4.ItemCollection)
        {
          ListItem listItem2 = unitDictionary[((FieldLookupValue) listItem1["Unit_x002f_Project_x003a_Project0"]).LookupValue];
          listItem1["Main_x0020_approver"] = listItem2["Main_x0020_approver"];
          listItem1["Optional_x0020_approver"] = listItem2["Optional_x0020_approver"];
          listItem1["Financial_x0020_approver"] = listItem2["Financial_x0020_approver"];
          listItem1.Update();
          ++num;
          if (num % 80 == 0)
            spoList4.Ctx.ExecuteQuery();
        }
        Console.WriteLine("Executing last query");
        spoList4.Ctx.ExecuteQuery();
        Console.WriteLine("Done Executing last query");
      }
      return true;
    }

    private string PadStr(int i)
    {
      return i < 10 ? "0" + (object) i : string.Concat((object) i);
    }

    private string QueryBuilder(Dictionary<string, ListItem> unitDictionary)
    {
      bool flag = true;
      string str = this.OrAppend("<View><Query><Where>", unitDictionary.Keys.Count);
      foreach (string key in unitDictionary.Keys)
      {
        str = str + "<Eq><FieldRef Name='Unit_x002f_Project_x003a_Project0' /><Value Type='Text'>" + key + "</Value></Eq>";
        if (!flag)
          str += "</Or>";
        flag = false;
      }
      return str + "</Where></Query></View>";
    }

    private string OrAppend(string str, int size)
    {
      string str1;
      if (size <= 1)
        str1 = str;
      else
        str = str1 = this.OrAppend(str + "<Or>", size - 1);
      return str1;
    }
  }
}
