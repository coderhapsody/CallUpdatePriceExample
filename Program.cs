using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CallUpdatePriceExample
{
    class Program
    {
        static void Main(string[] args)
        {
            // TODO: Prior to running this code, please configure the service URLs in app.config first.

            // Configure identity to connect to the web service.
            string userName = "adc\\p.iman";
            string password = "StrongPasswordHere";

            // Create instance to Horizon web service
            var ws = new CoreWS.PriceIndexWSSoapClient();
            ws.ClientCredentials.UserName.UserName = userName;
            ws.ClientCredentials.UserName.Password = password;

            // Prepare to load pricevalue rows
            var ds = new CoreWS.PriceIndexDS();
            var sc = new CoreWS.SelectCriteria();
            var dbCriteria = new List<CoreWS.DbCriteria>();
            // Specity the price index name that you want to load its pricevalue rows.
            dbCriteria.Add(new CoreWS.DbCriteria() { DbColumn = "priceindex", RelOperator = "=", DbValue = "AECC_CSWS_OFF_Round 1" });
            sc.DbCriteria = dbCriteria.ToArray();
            string[] tables = new[] { "priceindex", "pricevalue" };
            // Load pricevalue rows
            var resultDS = ws.RetrievePriceIndex(ds, tables, sc);


            // Create instance to the class event web service (the class event web service code must be imported and compiled first).
            ClassEventsWS.PriceValueWSSoapClient classEventWS = new ClassEventsWS.PriceValueWSSoapClient();
            classEventWS.ClientCredentials.UserName.UserName = userName;
            classEventWS.ClientCredentials.UserName.Password = password;

            // Create a neverending task that always modifies pricevalue rows.
            var cts = new CancellationTokenSource();
            var task = Task.Factory.StartNew(() =>
            {
                while(!cts.IsCancellationRequested)
                {
                    Console.WriteLine("Updating pricevalue rows, press any key to terminate.");

                    // Updating prices in the data table.
                    Array.ForEach(resultDS.Tables["pricevalue"].AsEnumerable().ToArray(), row => row["price"] = Convert.ToDecimal(row["price"]) * 1.1M);

                    // Call the web service in class event routine.
                    ClassEventsWS.PriceIndexDS.pricevalueDataTable updateTable = new ClassEventsWS.PriceIndexDS.pricevalueDataTable();
                    foreach(DataRow editRow in resultDS.Tables["pricevalue"].Rows)
                    {
                        updateTable.ImportRow(editRow);
                    }
                    var newRow = updateTable.NewpricevalueRow();
                    newRow.ItemArray = updateTable.Rows[0].ItemArray;
                    newRow.price = 999;
                    newRow.surrogate = -1;
                    updateTable.AddpricevalueRow(newRow);
                    updateTable.Merge(resultDS.Tables["pricevalue"]);
                    classEventWS.UpdatePrices(updateTable);

                    Thread.Sleep(3000);
                }
            }, cts.Token);
            task.ContinueWith(x => Console.WriteLine("Unable to update prices. Please verify the URL endpoint in app.config."), TaskContinuationOptions.OnlyOnFaulted);


            // Press any key to cancel the task.
            Console.ReadKey();
            cts.Cancel();
        }
    }
}
