// Decompiled with JetBrains decompiler
// Type: Bring.Sharepoint.Context
// Assembly: ConsoleApp1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 2529ACA9-9F81-4C49-8E47-E8B02D261367
// Assembly location: C:\Users\KEVIN\Desktop\Visual Studio\SPtoSP\ConsoleApp1.exe

using Microsoft.SharePoint.Client;
using System;
using System.Linq.Expressions;
using System.Net;

namespace Bring.Sharepoint
{
  public class Context
  {
    internal Web web;

    public string Site { get; set; }

    public SPOUser SPOUser { get; set; }

    public ClientContext Ctx { get; set; }

    public void BuildContext()
    {
      ClientContext clientContext = new ClientContext("https://bringglobal.sharepoint.com/" + this.Site);
      clientContext.Credentials = (ICredentials) this.SPOUser.spoCredentials;
      this.Ctx = clientContext;
      this.web = this.Ctx.Web;
      this.Ctx.Load<Web>(this.web, Array.Empty<Expression<Func<Web, object>>>());
    }

    public ListCollection GetAllLists()
    {
      if (this.web == null || this.Ctx.Site.Context.Url != "https://bringglobal.sharepoint.com/" + this.Site)
        this.BuildContext();
      ListCollection lists = this.web.Lists;
      this.Ctx.Load<ListCollection>(lists, Array.Empty<Expression<Func<ListCollection, object>>>());
      this.Ctx.ExecuteQuery();
      return lists;
    }
  }
}
