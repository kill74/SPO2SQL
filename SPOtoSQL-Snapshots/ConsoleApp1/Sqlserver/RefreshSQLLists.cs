// Decompiled with JetBrains decompiler
// Type: Bring.Sqlserver.RefreshSQLLists
// Assembly: ConsoleApp1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 2529ACA9-9F81-4C49-8E47-E8B02D261367
// Assembly location: C:\Users\KEVIN\Desktop\Visual Studio\SPtoSP\ConsoleApp1.exe

using Bring.Sharepoint;
using System;
using System.Configuration;
using System.Xml;

namespace Bring.Sqlserver
{
  internal class RefreshSQLLists
  {
    public static void SPOtoSQLUpdate(bool daily)
    {
      SPOUser user = new SPOUser("Guilherme.sales@bringglobal.com", "UtKid_3AV^r]H+"); // FJ -> APAGUEI AS CREDENCIAIS QUE ESTÂO EM USO
      foreach (string allKey in ConfigurationManager.AppSettings.AllKeys)
        RefreshSQLLists.RefreshListsSQL(allKey, ConfigurationManager.AppSettings[allKey], user, daily);
    }

    public static void RefreshListsSQL(string listName, string ctxURL, SPOUser user, bool daily)
    {
      try
      {
        SPOList spoList1 = new SPOList();
        spoList1.Site = ctxURL;
        spoList1.SPOUser = user;
        spoList1.Name = listName;
        SPOList spoList2 = spoList1;
        SQLInteraction sqlInteraction = new SQLInteraction()
        {
          List = spoList2
        };
        sqlInteraction.Build();
        if (daily)
          sqlInteraction.DailyUpdate();
        else
          sqlInteraction.CurrentTimeUpdate();
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }
  }
}
