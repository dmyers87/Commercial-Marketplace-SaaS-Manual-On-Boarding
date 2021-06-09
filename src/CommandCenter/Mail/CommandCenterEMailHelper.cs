﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CommandCenter.Mail
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CommandCenter.Marketplace;
    using CommandCenter.Models;
    using CommandCenter.Utilities;
    using Microsoft.Extensions.Options;
    using Microsoft.Marketplace.SaaS;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using SendGrid;
    using SendGrid.Helpers.Mail;

    /// <summary>
    /// Helper for email messages.
    /// </summary>
    public class CommandCenterEMailHelper : IMarketplaceNotificationHandler
    {
        private const string MailLinkControllerName = "MailLink";
        private const string NotProvidedValue = "Not Provided";

        private readonly IMarketplaceSaaSClient marketplaceClient;

        private readonly CommandCenterOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandCenterEMailHelper"/> class.
        /// </summary>
        /// <param name="optionsMonitor">Options monitor.</param>
        /// <param name="marketplaceClient">Marketplace API client.</param>
        public CommandCenterEMailHelper(
            IOptionsMonitor<CommandCenterOptions> optionsMonitor,
            IMarketplaceSaaSClient marketplaceClient)
        {
            if (optionsMonitor == null)
            {
                throw new ArgumentNullException(nameof(optionsMonitor));
            }

            this.marketplaceClient = marketplaceClient;
            this.options = optionsMonitor.CurrentValue;
        }

        /// <inheritdoc/>
        public async Task NotifyChangePlanAsync(
            WebhookPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            await this.SendWebhookNotificationEmailAsync(
                "Plan change request complete",
                $"Plan change request complete. Please take the required action.",
                string.Empty,
                payload,
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ProcessNewSubscriptionAsyc(
            AzureSubscriptionProvisionModel provisionModel,
            CancellationToken cancellationToken = default)
        {
            if (provisionModel == null)
            {
                throw new ArgumentNullException(nameof(provisionModel));
            }

            var queryParams = new List<Tuple<string, string>>
            {
                new Tuple<string, string>(
                    "subscriptionId",
                    provisionModel.SubscriptionId.ToString()),
                new Tuple<string, string>("planId", provisionModel.PlanId),
            };

            var emailText =
                "<p>New subscription. Please take the required action, then return to this email and click the following link to confirm. ";
            emailText += $"{this.BuildALink("Activate", queryParams, "Click here to activate subscription")}.</p>";
            emailText +=
                $"<div> <p> Details are</p> <div> {BuildTable(provisionModel)}</div></div>";

            await this.SendEmailAsync(
                () => $"New subscription, {provisionModel.SubscriptionName}",
                () => emailText,
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ProcessChangePlanAsync(
            AzureSubscriptionProvisionModel provisionModel,
            CancellationToken cancellationToken = default)
        {
            if (provisionModel == null)
            {
                throw new ArgumentNullException(nameof(provisionModel));
            }

            var queryParams = new List<Tuple<string, string>>
            {
                new Tuple<string, string>(
                    "subscriptionId",
                    provisionModel.SubscriptionId.ToString()),
                new Tuple<string, string>("planId", provisionModel.NewPlanId),
            };

            var emailText = $"<p>Updated subscription from {provisionModel.PlanId} to {provisionModel.NewPlanId}.";

            emailText +=
                "Please take the required action, then return to this email and click the following link to confirm. ";
            emailText += $"{this.BuildALink("Update", queryParams, "Click here to update subscription")}.</p>";
            emailText +=
                $"<div> <p> Details are</p> <div> {BuildTable(JObject.Parse(JsonConvert.SerializeObject(provisionModel)))}</div></div>";

            await this.SendEmailAsync(
                () => $"Update subscription, {provisionModel.SubscriptionName}",
                () => emailText,
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task NotifyChangeQuantityAsync(
            WebhookPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            await this.SendWebhookNotificationEmailAsync(
                "Quantity change request",
                "Quantity change request. Please take the required action.",
                string.Empty,
                payload,
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ProcessOperationFailOrConflictAsync(
            WebhookPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var notificationModel = NotificationModel.FromWebhookPayload(payload);

            var queryParams = new List<Tuple<string, string>>
            {
                new Tuple<string, string>(
                    "subscriptionId",
                    notificationModel.SubscriptionId.ToString()),
            };

            var subscriptionDetails = await this.marketplaceClient.Fulfillment.GetSubscriptionAsync(
                notificationModel.SubscriptionId,
                Guid.Empty,
                Guid.Empty,
                cancellationToken).ConfigureAwait(false);

            await this.SendEmailAsync(
                () => $"Operation failure, {subscriptionDetails.Value.Name}",
                () =>
                    $"<p>Operation failure. {this.BuildALink("Operations", queryParams, "Click here to list all operations for this subscription", "Subscriptions")}</p>. "
                    + $"<p> Details are {BuildTable(JObject.Parse(JsonConvert.SerializeObject(subscriptionDetails)))}</p>",
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task NotifyReinstatedAsync(
            WebhookPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            await this.SendWebhookNotificationEmailAsync(
                "Reinstate subscription request",
                "Reinstate subscription request. Please take the required action, then return to this email and click the following link to confirm.",
                "Reinstate",
                payload,
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task NotifySuspendedAsync(
            WebhookPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            await this.SendWebhookNotificationEmailAsync(
                "Suspend subscription request",
                "Suspend subscription request. Please take the required action.",
                string.Empty,
                payload,
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task NotifyUnsubscribedAsync(
            WebhookPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            await this.SendWebhookNotificationEmailAsync(
                "Cancel subscription request",
                "Cancel subscription request. Please take the required action.",
                string.Empty,
                payload,
                cancellationToken).ConfigureAwait(false);
        }

        private static string BuildTable(JObject parsed)
        {
            var tableContents = parsed.Properties().AsEnumerable()
                .Select(p => $"<tr><th align=\"left\"> {p.Name} </th><th align=\"left\"> {p.Value}</th></tr>")
                .Aggregate((head, tail) => head + tail);
            return $"<table border=\"1\" align=\"left\">{tableContents}</table>";
        }

        private static string BuildTable(AzureSubscriptionProvisionModel model)
        {
            var returnString = $"<table border=\"1\" align=\"left\">";

            if (model != null)
            {
                returnString += $"<tr><th align=\"left\">{model.GetDisplayName(nameof(model.Location))}</th><th align=\"left\"> {model.Location.GetDisplayName()}</th></tr>";
                returnString += $"<tr><th align=\"left\">{model.GetDisplayName(nameof(model.PlanId))}</th><th align=\"left\"> {model?.PlanId ?? NotProvidedValue}</th></tr>";
                if (model.CustomBundleOptions != null)
                {
                    var cbo = model.CustomBundleOptions;
                    returnString += $"<tr><th align=\"left\">{cbo.GetDisplayName(nameof(cbo.RequestedPerformanceTier))}</th><th align=\"left\"> {cbo.RequestedPerformanceTier.GetDisplayName()}</th></tr>";
                    returnString += $"<tr><th align=\"left\">{cbo.GetDisplayName(nameof(cbo.RequestedBandwidth))}</th><th align=\"left\"> {cbo?.RequestedBandwidth ?? NotProvidedValue}</th></tr>";
                    returnString += $"<tr><th align=\"left\">{cbo.GetDisplayName(nameof(cbo.RequestedStorageSize))}</th><th align=\"left\"> {cbo?.RequestedStorageSize ?? NotProvidedValue}</th></tr>";
                }

                returnString += $"<tr><th align=\"left\">{model.GetDisplayName(nameof(model.TechnicalContactName))}</th><th align=\"left\"> {model?.TechnicalContactName ?? NotProvidedValue}</th></tr>";
                returnString += $"<tr><th align=\"left\">{model.GetDisplayName(nameof(model.TechnicalContactEmail))}</th><th align=\"left\"> {model?.TechnicalContactEmail ?? NotProvidedValue}</th></tr>";
                returnString += $"<tr><th align=\"left\">{model.GetDisplayName(nameof(model.TechnicalContactPhone))}</th><th align=\"left\"> {model?.TechnicalContactPhone ?? NotProvidedValue}</th></tr>";
                if (model.TechnicalDetails != null)
                {
                    var techDetails = model.TechnicalDetails;
                    returnString += "<tr><th align=\"center\">Technical Details></th><th></th></tr>";
                    returnString += $"<tr><th align=\"left\">{techDetails.GetDisplayName(nameof(techDetails.RequestedShareName))}</th><th align=\"left\"> {techDetails?.RequestedShareName ?? NotProvidedValue}</th></tr>";
                    returnString += $"<tr><th align=\"left\">{techDetails.GetDisplayName(nameof(techDetails.SupportNFS))}</th><th align=\"left\"> {(techDetails.SupportNFS ? "Yes" : "No")}</th></tr>";
                    returnString += $"<tr><th align=\"left\">{techDetails.GetDisplayName(nameof(techDetails.SupportCIFS))}</th><th align=\"left\"> {(techDetails.SupportCIFS ? "Yes" : "No")}</th></tr>";
                    returnString += $"<tr><th align=\"left\">{techDetails.GetDisplayName(nameof(techDetails.RequestedSubnet))}</th><th align=\"left\"> {techDetails?.RequestedSubnet ?? NotProvidedValue}</th></tr>";
                }

                returnString += "<tr><th align=\"center\">Azure Details></th><th></th></tr>";
                returnString += $"<tr><th align=\"left\">{model.GetDisplayName(nameof(model.OfferId))}</th><th align=\"left\"> {model?.OfferId ?? NotProvidedValue}</th></tr>";
                returnString += $"<tr><th align=\"left\">{model.GetDisplayName(nameof(model.PurchaserTenantId))}</th><th align=\"left\"> {model.PurchaserTenantId}</th></tr>";
                returnString += $"<tr><th align=\"left\">{model.GetDisplayName(nameof(model.SubscriptionId))}</th><th align=\"left\"> {model.SubscriptionId}</th></tr>";
                returnString += $"<tr><th align=\"left\">{model.GetDisplayName(nameof(model.SubscriptionName))}</th><th align=\"left\"> {model?.SubscriptionName ?? NotProvidedValue}</th></tr>";
                returnString += $"<tr><th align=\"left\">{model.GetDisplayName(nameof(model.ExistingSpendCommitments))}</th><th align=\"left\"> {(model.ExistingSpendCommitments ? "Yes" : "No")}</th></tr>";

                returnString += $"</table>";
            }

            return returnString;
        }

        private string BuildALink(
            string controllerAction,
            IEnumerable<Tuple<string, string>> queryParams,
            string innerText,
            string controllerName = MailLinkControllerName)
        {
            var uriStart = FluentUriBuilder.Start(this.options.BaseUrl).AddPath(controllerName)
                .AddPath(controllerAction);

            foreach (var (item1, item2) in queryParams) uriStart.AddQuery(item1, item2);

            var href = uriStart.Uri.ToString();

            return $"<a href=\"{href}\">{innerText}</a>";
        }

        private async Task SendEmailAsync(
            Func<string> subjectBuilder,
            Func<string> contentBuilder,
            CancellationToken cancellationToken = default)
        {
            var msg = new SendGridMessage();

            msg.SetFrom(new EmailAddress(this.options.Mail.FromEmail, "Marketplace command center"));

            var recipients = new List<EmailAddress> { new EmailAddress(this.options.Mail.OperationsTeamEmail) };

            msg.AddTos(recipients);

            msg.SetSubject(subjectBuilder());

            msg.AddContent(MimeType.Html, contentBuilder());

            var client = new SendGridClient(this.options.Mail.ApiKey);
            var response = await client.SendEmailAsync(msg, cancellationToken).ConfigureAwait(false);

            if ((response.StatusCode != System.Net.HttpStatusCode.OK) &&
                (response.StatusCode != System.Net.HttpStatusCode.Accepted))
            {
                throw new ApplicationException(await response.Body.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
        }

        private async Task SendWebhookNotificationEmailAsync(
            string subject,
            string mailBody,
            string actionName,
            WebhookPayload payload,
            CancellationToken cancellationToken)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var notificationModel = NotificationModel.FromWebhookPayload(payload);

            if (string.IsNullOrEmpty(actionName))
            {
                var queryParams = new List<Tuple<string, string>>
                {
                    new Tuple<string, string>(
                        "subscriptionId",
                        notificationModel.SubscriptionId.ToString()),
                    new Tuple<string, string>("publisherId", notificationModel.PublisherId),
                    new Tuple<string, string>("offerId", notificationModel.OfferId),
                    new Tuple<string, string>("planId", notificationModel.PlanId),
                    new Tuple<string, string>("quantity", notificationModel.Quantity.ToString(CultureInfo.InvariantCulture)),
                    new Tuple<string, string>("operationId", notificationModel.OperationId.ToString()),
                };

                var actionLink = !string.IsNullOrEmpty(actionName)
                    ? this.BuildALink(actionName, queryParams, "Click here to confirm.")
                    : string.Empty;

                mailBody = $"{mailBody}" + $"{actionLink}";
            }

            var subscriptionDetails = await this.marketplaceClient.Fulfillment.GetSubscriptionAsync(
                notificationModel.SubscriptionId,
                Guid.Empty,
                Guid.Empty,
                cancellationToken).ConfigureAwait(false);

            await this.SendEmailAsync(
                () => $"{subject}, {subscriptionDetails.Value.Name}",
                () => $"<p>{mailBody}</p>"
                                       + $"<br/><div> Details are {BuildTable(JObject.Parse(JsonConvert.SerializeObject(subscriptionDetails)))}</div>",
                cancellationToken).ConfigureAwait(false);
        }
    }
}
