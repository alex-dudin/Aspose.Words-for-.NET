﻿using Aspose.Words;
using System.Data;

namespace _04._01_MailMergefromXMLDataSource
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create the Dataset and read the XML.
            DataSet customersDs = new DataSet();
            customersDs.ReadXml("../../data/Customers.xml");

            // Open a template document.
            Document doc = new Document("../../data/TestFile XML.doc");

            // Execute mail merge to fill the template with data from XML using DataTable.
            doc.MailMerge.Execute(customersDs.Tables["Customer"]);

            // Save the output document.
            doc.Save("MailMergefromXMLDataSource.docx");
        }
    }
}
