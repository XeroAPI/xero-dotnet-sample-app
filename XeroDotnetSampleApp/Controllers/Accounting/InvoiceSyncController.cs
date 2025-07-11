using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xero.NetStandard.OAuth2.Model.Accounting;
using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Config;
using Microsoft.Extensions.Options;
using System.Linq;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace XeroDotnetSampleApp.Controllers
{
    /// <summary>
    /// Controller implementing methods demonstrating following Invoice endpoints:
    /// <para>- GET: /InvoiceSync/</para>
    /// <para>- POST: /InvoiceSync#Create/</para>
    /// <para>- POST: /InvoiceSync/FileUpload#Upload</para>
    /// </summary>
    public class InvoiceSync : ApiAccessorController<AccountingApi>
    {
        public InvoiceSync(IOptions<XeroConfiguration> xeroConfig):base(xeroConfig){}

        #region GET Endpoints
        /// <summary>
        /// GET: /InvoiceSync/
        /// </summary>
        /// <returns>Returns list of invoices for last 7 days</returns>
        public async Task<IActionResult> Index()
        {
            // Call get invoices endpoint
            var response = await Api.GetInvoicesAsync(XeroToken.AccessToken, TenantId, where: GetSevenDayInvoiceFilter());

            ViewBag.jsonResponse = response.ToJson();
            return View(response._Invoices);
        }

        /// <summary>
        /// GET: /InvoiceSync#Create
        /// <para>Helper method to return View</para>
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        #endregion

        #region POST Endpoints
        /// <summary>
        /// POST: /InvoiceSync#Create
        /// </summary>
        /// <param name="name">Name of invoice</param>
        /// <param name="lineDescription">Description of line item</param>
        /// <param name="lineQuantity">Quantity of line item</param>
        /// <param name="lineUnitAmount">Unit amount of line item</param>
        /// <param name="lineAccountCode">Line item account code</param>
        /// <returns>Returns action result to redirect user to get invoices page</returns>
        [HttpPost]
        public async Task<IActionResult> Create(string name, string lineDescription, string lineQuantity,
            string lineUnitAmount, string lineAccountCode)
        {
            // Construct invoice
            var invoice = ConstructInvoice(name, lineDescription, lineQuantity, lineUnitAmount, lineAccountCode);
            var invoices = new Invoices
            {
                _Invoices = new List<Invoice> { invoice }
            };

            // Call create invoice endpoint
            await Api.CreateInvoicesAsync(XeroToken.AccessToken, TenantId, invoices);

            return RedirectToAction("Index", "InvoiceSync");
        }

        /// <summary>
        /// POST: /InvoiceSync/FileUpload#Upload
        /// </summary>
        /// <param name="files">File to upload to specified invoice</param>
        /// <param name="invoiceId">Invoice id to associate file with</param>
        /// <returns>Returns information for account files</returns>
        [HttpPost("InvoiceFileUpload")]
        public async Task<IActionResult> Upload(List<IFormFile> files, Guid invoiceId)
        {
            // Setup
            var size = files.Sum(f => f.Length);
            var filePaths = new List<string>();

            foreach (var formFile in files)
            {
                if (formFile.Length > 0)
                {
                    var filePath = Path.GetTempFileName();
                    filePaths.Add(filePath);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        await formFile.CopyToAsync(ms);
                        ms.Position = 0;
                        
                        await Api.CreateInvoiceAttachmentByFileNameAsync(XeroToken.AccessToken, TenantId, invoiceId, formFile.FileName, ms);
                    }
                }
            }

            return Ok(new { count = files.Count, size, filePaths });
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Generate invoice filter string for datetime seven days ago
        /// </summary>
        /// <returns>Formatted invoice filter string</returns>
        private string GetSevenDayInvoiceFilter()
        {
            var sevenDaysAgo = DateTime.Now.AddDays(-7).ToString("yyyy, MM, dd");
            return "Date >= DateTime(" + sevenDaysAgo + ")";
        }

        /// <summary>
        /// Generate invoice object
        /// </summary>
        /// <param name="name">Name of invoice</param>
        /// <param name="lineDescription">Description of line item</param>
        /// <param name="lineQuantity">Quantity of line item</param>
        /// <param name="lineUnitAmount">Unit amount of line item</param>
        /// <param name="lineAccountCode">Line item account code</param>
        /// <returns>Returns instantiated invoice object</returns>
        private Invoice ConstructInvoice(string name, string lineDescription, string lineQuantity, string lineUnitAmount, string lineAccountCode)
        {
            var contact = new Contact { Name = name };
            var lineItems = new List<LineItem>
            {
                new LineItem
                {
                    Description = lineDescription,
                    Quantity = decimal.Parse(lineQuantity),
                    UnitAmount = decimal.Parse(lineUnitAmount),
                    AccountCode = lineAccountCode,
                }
            };
            return new Invoice
            {
                Type = Invoice.TypeEnum.ACCREC,
                Contact = contact,
                Date = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                LineItems = lineItems
            };
        }
        #endregion
    }
}