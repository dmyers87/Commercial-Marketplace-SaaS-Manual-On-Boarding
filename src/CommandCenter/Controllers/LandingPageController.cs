﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CommandCenter.Controllers
{
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading;
    using System.Threading.Tasks;
    using CommandCenter.Authorization;
    using CommandCenter.Marketplace;
    using CommandCenter.Models;
    using CommandCenter.Persistance;
    using Microsoft.AspNetCore.Authentication.OpenIdConnect;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.Marketplace.SaaS;
    using Microsoft.Marketplace.SaaS.Models;

    /// <summary>
    /// Landing page.
    /// </summary>
    [Authorize(AuthenticationSchemes = OpenIdConnectDefaults.AuthenticationScheme)]

    // Specify the auth scheme to be used for logging on users. This is for supporting WebAPI auth
    public class LandingPageController : Controller
    {
        private readonly ILogger<LandingPageController> logger;
        private readonly IMarketplaceProcessor marketplaceProcessor;
        private readonly IMarketplaceNotificationHandler notificationHandler;
        private readonly IMarketplaceSaaSClient marketplaceClient;
        private readonly CommandCenterOptions options;
        private readonly IRequestPersistenceStore requestPersistenceStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="LandingPageController"/> class.
        /// </summary>
        /// <param name="commandCenterOptions">Options.</param>
        /// <param name="marketplaceProcessor">Marketplace processor.</param>
        /// <param name="notificationHandler">Notification handler.</param>
        /// <param name="marketplaceClient">Marketplace client.</param>
        /// <param name="requestPersistenceStore">Request Persistence Store.</param>
        /// <param name="logger">Logger.</param>
        public LandingPageController(
            IOptionsMonitor<CommandCenterOptions> commandCenterOptions,
            IMarketplaceProcessor marketplaceProcessor,
            IMarketplaceNotificationHandler notificationHandler,
            IMarketplaceSaaSClient marketplaceClient,
            IRequestPersistenceStore requestPersistenceStore,
            ILogger<LandingPageController> logger)
        {
            if (commandCenterOptions == null)
            {
                throw new ArgumentNullException(nameof(commandCenterOptions));
            }

            this.marketplaceProcessor = marketplaceProcessor;
            this.notificationHandler = notificationHandler;
            this.marketplaceClient = marketplaceClient;
            this.logger = logger;
            this.options = commandCenterOptions.CurrentValue;
            this.requestPersistenceStore = requestPersistenceStore;
        }

        /// <summary>
        /// Landing page get.
        /// </summary>
        /// <param name="token">Marketplace purchase identification token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Action result.</returns>
        public async Task<ActionResult> Index(string token, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(token))
            {
                this.ModelState.AddModelError(string.Empty, "Token URL parameter cannot be empty");
                this.ViewBag.Message = "Token URL parameter cannot be empty";
                return this.View();
            }

            // Get the subscription for the offer from the marketplace purchase identification token
            var resolvedSubscription = await this.marketplaceProcessor.GetSubscriptionFromPurchaseIdentificationTokenAsync(token, cancellationToken).ConfigureAwait(false);

            // Rest is implementation detail. In this sample, we chose allow the subscriber to change the plan for an activated subscriptio
            if (resolvedSubscription == default(ResolvedSubscription))
            {
                this.ViewBag.Message = "Token did not resolve to a subscription";
                return this.View();
            }

            // resolvedSubscription.Subscription is null when calling mock endpoint
            var existingSubscription = resolvedSubscription.Subscription;

            // var availablePlans = await this.marketplaceClient.Fulfillment.ListAvailablePlansAsync(
            //    resolvedSubscription.Id.Value,
            //    null,
            //    null,
            //    cancellationToken).ConfigureAwait(false);
            var existingRequest = await this.requestPersistenceStore.GetRequestBySubscriptionIdAsync(resolvedSubscription.Id.Value);
            if (existingRequest != null)
            {
                return this.RedirectToAction(nameof(this.RequestSubmitted));
            }

            var pendingOperations = await this.marketplaceClient.Operations.ListOperationsAsync(
                resolvedSubscription.Id.Value,
                null,
                null,
                cancellationToken).ConfigureAwait(false);

            var provisioningModel = new AzureSubscriptionProvisionModel
            {
                PlanId = resolvedSubscription.PlanId,
                SubscriptionId = resolvedSubscription.Id.Value,
                OfferId = resolvedSubscription.OfferId,
                SubscriptionName = resolvedSubscription.SubscriptionName,
                PurchaserEmail = existingSubscription?.Purchaser?.EmailId,
                PurchaserTenantId = existingSubscription?.Purchaser?.TenantId ?? Guid.Empty,

                // Assuming this will be set to the value the customer already set when subscribing, if we are here after the initial subscription activation
                // Landing page is used both for initial provisioning and configuration of the subscription.
                SubscriptionStatus = existingSubscription?.SaasSubscriptionStatus ?? SubscriptionStatusEnum.NotStarted,
                PendingOperations = pendingOperations?.Value.Operations?.Any(o => o.Status == OperationStatusEnum.InProgress) ?? false,
            };

            if (provisioningModel != default)
            {
                provisioningModel.FullName = (this.User.Identity as ClaimsIdentity)?.FindFirst("name")?.Value;
                provisioningModel.Email = this.User.Identity.GetUserEmail();
                provisioningModel.BusinessUnitContactEmail = this.User.Identity.GetUserEmail();

                if (string.Equals(provisioningModel.PlanId, "custom_bundle", StringComparison.InvariantCultureIgnoreCase))
                {
                    provisioningModel.CustomBundleOptions = new FactionCustomBundleModel();
                }
                else
                {
                    provisioningModel.CustomBundleOptions = null;
                }

                this.TryValidateModel(provisioningModel);
                return this.View(provisioningModel);
            }

            this.ModelState.AddModelError(string.Empty, "Cannot resolve subscription");
            return this.View();

            // This is just for testing or Demo purposes
            // var model = new AzureSubscriptionProvisionModel();
            // model.CustomBundleOptions = new FactionCustomBundleModel();
            // this.TryValidateModel(model);
            // return this.View(model);
        }

        /// <summary>
        /// Landing page post handler.
        /// </summary>
        /// <param name="provisionModel">View model.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Action result.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(AzureSubscriptionProvisionModel provisionModel, CancellationToken cancellationToken)
        {
            if (provisionModel == null)
            {
                throw new ArgumentNullException(nameof(provisionModel));
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(provisionModel);
            }
            else
            {
                var result = await this.requestPersistenceStore.InsertRequestAsync(provisionModel);

                var urlBase = $"{this.Request.Scheme}://{this.Request.Host}";
                this.options.BaseUrl = new Uri(urlBase);
                try
                {
                    // A new subscription will have PendingFulfillmentStart as status
                    if (provisionModel.SubscriptionStatus == SubscriptionStatusEnum.PendingFulfillmentStart)
                    {
                        await this.notificationHandler.ProcessNewSubscriptionAsyc(provisionModel, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await this.notificationHandler.ProcessChangePlanAsync(provisionModel, cancellationToken).ConfigureAwait(false);
                    }

                    return this.RedirectToAction(nameof(this.Success));
                }
                catch (Exception ex)
                {
                    return this.BadRequest(ex);
                }
            }
        }

        /// <summary>
        /// Success.
        /// </summary>
        /// <returns>Action result.</returns>
        public ActionResult Success()
        {
            return this.View();
        }

        /// <summary>
        /// Request Submitted.
        /// </summary>
        /// <returns>Action result.</returns>
        public ActionResult RequestSubmitted()
        {
            return this.View();
        }
    }
}
