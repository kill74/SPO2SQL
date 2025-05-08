// Decompiled with JetBrains decompiler
// Type: Bring.Sharepoint.SPOUser
// Assembly: ConsoleApp1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 2529ACA9-9F81-4C49-8E47-E8B02D261367
// Assembly location: C:\Users\KEVIN\Desktop\Visual Studio\SPtoSP\ConsoleApp1.exe

using Microsoft.SharePoint.Client;
using System.Security;
using System;
using System.Xml;

namespace Bring.Sharepoint
{
  public class SPOUser
  {
    public string username;
    private SecureString securePW;
    internal SharePointOnlineCredentials spoCredentials;

    public SPOUser(string username, string password)
    {
        Console.WriteLine("user=" + username + " pass=" + password); 
        this.username = username;
        this.securePW = new SecureString();
        foreach (char c in password.ToCharArray())
            this.securePW.AppendChar(c);
        this.spoCredentials = new SharePointOnlineCredentials(username, this.securePW);
    }
  }
}
