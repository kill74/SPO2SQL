// Decompiled with JetBrains decompiler
// Type: Bring.Sharepoint.SPOList
// Assembly: ConsoleApp1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 2529ACA9-9F81-4C49-8E47-E8B02D261367
// Assembly location: C:\Users\KEVIN\Desktop\Visual Studio\SPtoSP\ConsoleApp1.exe

using Microsoft.SharePoint.Client;
using System;
using System.Linq.Expressions;

namespace Bring.Sharepoint
{
  public class SPOList : Context
  {
    private List list;

    public string Name { get; set; }

    public ListItemCollection ItemCollection { get; set; }

    public FieldCollection Fields { get; set; }

    public string CAMLQuery { get; set; }

    public void Build()
    {
      if (this.web == null || this.Ctx.Site.Context.Url != "https://bringglobal.sharepoint.com/" + this.Site)
        this.BuildContext();
      CamlQuery camlQuery;
      if (this.CAMLQuery == null)
      {
        camlQuery = CamlQuery.CreateAllItemsQuery();
      }
      else
      {
        camlQuery = new CamlQuery();
        camlQuery.ViewXml = this.CAMLQuery;
      }
      CamlQuery query = camlQuery;
      this.list = this.web.Lists.GetByTitle(this.Name);
      this.ItemCollection = this.list.GetItems(query);
      this.Fields = this.list.Fields;
      this.Ctx.Load<List>(this.list, Array.Empty<Expression<Func<List, object>>>());
      this.Ctx.Load<ListItemCollection>(this.ItemCollection, Array.Empty<Expression<Func<ListItemCollection, object>>>());
      this.Ctx.Load<FieldCollection>(this.Fields, Array.Empty<Expression<Func<FieldCollection, object>>>());
      this.Ctx.ExecuteQuery();
    }

    public void Update()
    {
      this.Ctx.ExecuteQuery();
      this.Build();
    }

    public void PropsToString(ListItem item)
    {
      Console.WriteLine("Field|InternalName|Value|CBD|Hidden|FieldType|ReadOnly|FromBaseType|Required|ItemValueType");
      bool flag;
      foreach (Field field in (ClientObjectCollection<Field>) this.Fields)
      {
        try
        {
          object obj = item[field.InternalName];
          string title = field.Title;
          flag = obj == null;
          string str = flag.ToString();
          Console.WriteLine("We good for: " + title + " .   Is null: " + str);
          if (obj != null)
          {
            if (obj.GetType() == typeof (FieldLookupValue))
            {
              object[] objArray = new object[19];
              objArray[0] = (object) field.Title;
              objArray[1] = (object) "|";
              objArray[2] = (object) field.InternalName;
              objArray[3] = (object) "|";
              objArray[4] = (object) ((FieldLookupValue) obj).LookupValue;
              objArray[5] = (object) "|";
              flag = field.CanBeDeleted;
              objArray[6] = (object) flag.ToString();
              objArray[7] = (object) "|";
              flag = field.Hidden;
              objArray[8] = (object) flag.ToString();
              objArray[9] = (object) "|";
              objArray[10] = (object) field.TypeAsString;
              objArray[11] = (object) "|";
              flag = field.ReadOnlyField;
              objArray[12] = (object) flag.ToString();
              objArray[13] = (object) "|";
              flag = field.FromBaseType;
              objArray[14] = (object) flag.ToString();
              objArray[15] = (object) "|";
              flag = field.Required;
              objArray[16] = (object) flag.ToString();
              objArray[17] = (object) "|";
              objArray[18] = (object) obj.GetType();
              Console.WriteLine(string.Concat(objArray));
            }
            else if (obj.GetType() == typeof (FieldUserValue))
            {
              object[] objArray = new object[19];
              objArray[0] = (object) field.Title;
              objArray[1] = (object) "|";
              objArray[2] = (object) field.InternalName;
              objArray[3] = (object) "|";
              objArray[4] = (object) ((FieldLookupValue) obj).LookupId;
              objArray[5] = (object) "|";
              flag = field.CanBeDeleted;
              objArray[6] = (object) flag.ToString();
              objArray[7] = (object) "|";
              flag = field.Hidden;
              objArray[8] = (object) flag.ToString();
              objArray[9] = (object) "|";
              objArray[10] = (object) field.TypeAsString;
              objArray[11] = (object) "|";
              flag = field.ReadOnlyField;
              objArray[12] = (object) flag.ToString();
              objArray[13] = (object) "|";
              flag = field.FromBaseType;
              objArray[14] = (object) flag.ToString();
              objArray[15] = (object) "|";
              flag = field.Required;
              objArray[16] = (object) flag.ToString();
              objArray[17] = (object) "|";
              objArray[18] = (object) obj.GetType();
              Console.WriteLine(string.Concat(objArray));
            }
            else if (obj.GetType() == typeof (FieldLookupValue[]))
            {
              foreach (FieldLookupValue fieldLookupValue in (FieldLookupValue[]) obj)
              {
                object[] objArray = new object[19];
                objArray[0] = (object) field.Title;
                objArray[1] = (object) "|";
                objArray[2] = (object) field.InternalName;
                objArray[3] = (object) "|";
                objArray[4] = (object) fieldLookupValue.LookupId;
                objArray[5] = (object) "|";
                flag = field.CanBeDeleted;
                objArray[6] = (object) flag.ToString();
                objArray[7] = (object) "|";
                flag = field.Hidden;
                objArray[8] = (object) flag.ToString();
                objArray[9] = (object) "|";
                objArray[10] = (object) field.TypeAsString;
                objArray[11] = (object) "|";
                flag = field.ReadOnlyField;
                objArray[12] = (object) flag.ToString();
                objArray[13] = (object) "|";
                flag = field.FromBaseType;
                objArray[14] = (object) flag.ToString();
                objArray[15] = (object) "|";
                flag = field.Required;
                objArray[16] = (object) flag.ToString();
                objArray[17] = (object) "|";
                objArray[18] = (object) obj.GetType();
                Console.WriteLine(string.Concat(objArray));
              }
            }
            else
            {
              object[] objArray = new object[19];
              objArray[0] = (object) field.Title;
              objArray[1] = (object) "|";
              objArray[2] = (object) field.InternalName;
              objArray[3] = (object) "|";
              objArray[4] = obj;
              objArray[5] = (object) "|";
              flag = field.CanBeDeleted;
              objArray[6] = (object) flag.ToString();
              objArray[7] = (object) "|";
              flag = field.Hidden;
              objArray[8] = (object) flag.ToString();
              objArray[9] = (object) "|";
              objArray[10] = (object) field.TypeAsString;
              objArray[11] = (object) "|";
              flag = field.ReadOnlyField;
              objArray[12] = (object) flag.ToString();
              objArray[13] = (object) "|";
              flag = field.FromBaseType;
              objArray[14] = (object) flag.ToString();
              objArray[15] = (object) "|";
              flag = field.Required;
              objArray[16] = (object) flag.ToString();
              objArray[17] = (object) "|";
              objArray[18] = (object) obj.GetType();
              Console.WriteLine(string.Concat(objArray));
            }
          }
          else
          {
            object[] objArray = new object[17];
            objArray[0] = (object) field.Title;
            objArray[1] = (object) "|";
            objArray[2] = (object) field.InternalName;
            objArray[3] = (object) "|NULL|";
            flag = field.CanBeDeleted;
            objArray[4] = (object) flag.ToString();
            objArray[5] = (object) "|";
            flag = field.Hidden;
            objArray[6] = (object) flag.ToString();
            objArray[7] = (object) "|";
            objArray[8] = (object) field.TypeAsString;
            objArray[9] = (object) "|";
            flag = field.ReadOnlyField;
            objArray[10] = (object) flag.ToString();
            objArray[11] = (object) "|";
            flag = field.FromBaseType;
            objArray[12] = (object) flag.ToString();
            objArray[13] = (object) "|";
            flag = field.Required;
            objArray[14] = (object) flag.ToString();
            objArray[15] = (object) "|";
            objArray[16] = (object) obj.GetType();
            Console.WriteLine(string.Concat(objArray));
          }
        }
        catch (Exception ex)
        {
          string[] strArray = new string[16];
          strArray[0] = field.Title;
          strArray[1] = "|";
          strArray[2] = field.InternalName;
          strArray[3] = "|ERROR|";
          strArray[4] = field.CanBeDeleted.ToString();
          strArray[5] = "|";
          flag = field.Hidden;
          strArray[6] = flag.ToString();
          strArray[7] = "|";
          strArray[8] = field.TypeAsString;
          strArray[9] = "|";
          flag = field.ReadOnlyField;
          strArray[10] = flag.ToString();
          strArray[11] = "|";
          flag = field.FromBaseType;
          strArray[12] = flag.ToString();
          strArray[13] = "|";
          flag = field.Required;
          strArray[14] = flag.ToString();
          strArray[15] = "|CANT GET TYPE";
          Console.WriteLine(string.Concat(strArray));
          Console.WriteLine(ex.Message);
        }
      }
    }

    public ListItem AddItem()
    {
      ListItem listItem = this.list.AddItem(new ListItemCreationInformation());
      listItem.Update();
      return listItem;
    }
  }
}
