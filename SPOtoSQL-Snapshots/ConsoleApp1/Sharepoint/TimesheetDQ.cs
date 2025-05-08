// Decompiled with JetBrains decompiler
// Type: Bring.Sharepoint.TimesheetDQ
// Assembly: ConsoleApp1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 2529ACA9-9F81-4C49-8E47-E8B02D261367
// Assembly location: C:\Users\KEVIN\Desktop\Visual Studio\SPtoSP\ConsoleApp1.exe

using Microsoft.SharePoint.Client;
using System;

namespace Bring.Sharepoint
{
  internal class TimesheetDQ
  {
    public SPOUser Me { get; set; }

    public bool UpdateApprovers()
    {
      string str = "<View><Query><Where><IsNull><FieldRef Name ='Main_x0020_approver' /></IsNull></Where></Query></View>";
      SPOList spoList1 = new SPOList();
      spoList1.Name = "Timesheet";
      spoList1.Site = "selfservice/timesheet";
      spoList1.SPOUser = this.Me;
      spoList1.CAMLQuery = str;
      SPOList spoList2 = spoList1;
      spoList2.Build();
      foreach (ListItem listItem in (ClientObjectCollection<ListItem>) spoList2.ItemCollection)
      {
        try
        {
          ListItem unitItem = this.GetUnitItem(listItem);
          if (unitItem != null && unitItem["Main_x0020_approver"] != null)
          {
            listItem["Main_x0020_approver"] = unitItem["Main_x0020_approver"];
            listItem["Optional_x0020_approver"] = unitItem["Optional_x0020_approver"];
          }
          else
            listItem["Main_x0020_approver"] = (object) this.GetStructureApprover(listItem);
          listItem.Update();
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
          Console.WriteLine("Item ID: " + (object) listItem.Id);
        }
      }
      spoList2.Ctx.ExecuteQuery();
      return true;
    }

    private ListItem GetUnitItem(ListItem item)
    {
      SPOList spoList1 = new SPOList();
      spoList1.Name = "Unit";
      spoList1.Site = "seed";
      spoList1.SPOUser = this.Me;
      SPOList spoList2 = spoList1;
      string str = "<View><Query><Where><And><Eq><FieldRef Name ='Project_x0020_ID' /><Value Type='Text'>" + ((FieldLookupValue) item["Unit_x003a_Project_x0020_ID"]).LookupValue + "</Value></Eq><Eq><FieldRef Name ='Active' /><Value Type='Text'>Yes</Value></Eq></And></Where></Query></View>";
      spoList2.CAMLQuery = str;
      spoList2.Build();
      return spoList2.ItemCollection.Count != 0 ? spoList2.ItemCollection[0] : (ListItem) null;
    }

    private FieldUserValue GetStructureApprover(ListItem item)
    {
      SPOList spoList1 = new SPOList();
      spoList1.Name = "HR Database";
      spoList1.Site = "people";
      spoList1.SPOUser = this.Me;
      SPOList spoList2 = spoList1;
      string str = "<View><Query><Where><Eq><FieldRef Name ='Display_x0020_Name' LookupId='TRUE'/><Value Type='Integer'>" + (object) ((FieldLookupValue) item["Resource"]).LookupId + "</Value></Eq></Where></Query></View>";
      spoList2.CAMLQuery = str;
      spoList2.Build();
      return spoList2.ItemCollection.Count != 0 ? (FieldUserValue) spoList2.ItemCollection[0]["Approver1"] : (FieldUserValue) null;
    }
  }
}
