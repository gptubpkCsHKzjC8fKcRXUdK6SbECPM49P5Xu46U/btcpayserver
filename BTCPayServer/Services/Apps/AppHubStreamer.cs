using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Controllers;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using Microsoft.AspNetCore.SignalR;

namespace BTCPayServer.Services.Apps
{
    public class AppHubStreamer : EventHostedServiceBase
    {
        private readonly AppService _appService;
        private readonly IHubContext<AppHub> _HubContext;

        public AppHubStreamer(EventAggregator eventAggregator,
           IHubContext<AppHub> hubContext,
           AppService appService) : base(eventAggregator)
        {
            _appService = appService;
            _HubContext = hubContext;
        }

        protected override void SubscribeToEvents()
        {
            Subscribe<InvoiceEvent>();
            Subscribe<AppsController.AppUpdated>();
        }

        protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
        {
            if (evt is InvoiceEvent invoiceEvent)
            {
                foreach (var appId in AppService.GetAppInternalTags(invoiceEvent.Invoice))
                {
                    if (invoiceEvent.Name == InvoiceEvent.ReceivedPayment ||
                        invoiceEvent.Name == InvoiceEvent.MarkedCompleted ||
                        invoiceEvent.Name == InvoiceEvent.MarkedInvalid)
                    {
                        var data = invoiceEvent.Payment?.GetCryptoPaymentData();
                        if (data != null)
                        {


                            await _HubContext.Clients.Group(appId).SendCoreAsync(AppHub.PaymentReceived,
                                new object[]
                                {
                                    data.GetValue(), invoiceEvent.Payment.GetCryptoCode(),
                                    invoiceEvent.Payment.GetPaymentMethodId()?.PaymentType?.ToString()
                                }, cancellationToken);
                        }
                    }

                    await InfoUpdated(appId);
                }
            }
            else if (evt is AppsController.AppUpdated app)
            {
                await InfoUpdated(app.AppId);
            }
        }

        private async Task InfoUpdated(string appId)
        {
            var info = await _appService.GetAppInfo(appId);
            await _HubContext.Clients.Group(appId).SendCoreAsync(AppHub.InfoUpdated, new object[] { info });
        }
    }
}
