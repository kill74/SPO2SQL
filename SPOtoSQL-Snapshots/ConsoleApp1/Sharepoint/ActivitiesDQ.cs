// Decompiled with JetBrains decompiler
// Type: Bring.Sharepoint.ActivitiesDQ
// Assembly: ConsoleApp1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 2529ACA9-9F81-4C49-8E47-E8B02D261367
// Assembly location: C:\Users\KEVIN\Desktop\Visual Studio\SPtoSP\ConsoleApp1.exe

using Microsoft.SharePoint.Client;

namespace Bring.Sharepoint
{
  internal class ActivitiesDQ
  {
    public SPOUser Me { get; set; }

    public bool UpdateIDs()
    {
      string str = "<View><Query><Where><IsNull><FieldRef Name ='_OpportunityID' /></IsNull></Where></Query></View>";
      SPOList spoList1 = new SPOList();
      spoList1.Name = "activities";
      spoList1.Site = "wolf";
      spoList1.SPOUser = this.Me;
      spoList1.CAMLQuery = str;
      SPOList spoList2 = spoList1;
      spoList2.Build();
      foreach (ListItem listItem in (ClientObjectCollection<ListItem>) spoList2.ItemCollection)
      {
        listItem["_OpportunityID"] = listItem["OpportunityID"];
        listItem.Update();
      }
      spoList2.Ctx.ExecuteQuery();
      return true;
    }
  }
}
