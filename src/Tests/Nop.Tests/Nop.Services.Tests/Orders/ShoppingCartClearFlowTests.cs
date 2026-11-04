using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Shipping;
using Nop.Core.Http;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Attributes;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Events;
using Nop.Services.Helpers;
using Nop.Services.Html;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Web.Controllers;
using Nop.Web.Factories;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Routing;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.Orders;

//NOPCOM-SDLC-20261104-001
[TestFixture]
public class ShoppingCartClearFlowTests : ServiceTest
{
    [Test]
    public async Task ClearCart_RemovesCurrentStoreItems_ResetsCheckoutData_AndKeepsWishlistStashAndOtherStore()
    {
        var customer = await InsertGuestAsync();
        var store = await GetService<IStoreContext>().GetCurrentStoreAsync();
        var otherStoreId = store.Id + 50;

        try
        {
            await InsertItemAsync(customer, store.Id, ShoppingCartType.ShoppingCart, productId: 1);
            await InsertItemAsync(customer, store.Id, ShoppingCartType.Wishlist, productId: 2);
            await InsertItemAsync(customer, store.Id, ShoppingCartType.Stash, productId: 1);
            await InsertItemAsync(customer, otherStoreId, ShoppingCartType.ShoppingCart, productId: 2);
            await SeedCheckoutDataAsync(customer, store.Id);

            ClearShoppingCartEventConsumer.Reset();
            var result = await CreateController(customer).ClearCart();

            RedirectName(result).Should().Be(NopRouteNames.General.CART);
            (await LoadAsync(customer, ShoppingCartType.ShoppingCart, store.Id)).Should().BeEmpty();
            (await LoadAsync(customer, ShoppingCartType.Wishlist, store.Id)).Should().ContainSingle();
            (await LoadAsync(customer, ShoppingCartType.Stash, store.Id)).Should().ContainSingle();
            (await LoadAsync(customer, ShoppingCartType.ShoppingCart, otherStoreId)).Should().ContainSingle();
            (await GetService<ICustomerService>().GetCustomerByIdAsync(customer.Id)).HasShoppingCartItems.Should().BeTrue();
            ClearShoppingCartEventConsumer.HandleCount.Should().Be(1);
            ClearShoppingCartEventConsumer.LastCartCount.Should().Be(1);
            await AssertCheckoutDataClearedAsync(customer, store.Id);
        }
        finally
        {
            await DeleteCustomerCartAsync(customer);
        }
    }

    [Test]
    public async Task ClearCart_WhenCartsAreShared_RemovesItemsInEveryStore()
    {
        var customer = await InsertGuestAsync();
        var store = await GetService<IStoreContext>().GetCurrentStoreAsync();

        try
        {
            await WithSettingsAsync(settings => settings.CartsSharedBetweenStores = true, async () =>
            {
                await InsertItemAsync(customer, store.Id, ShoppingCartType.ShoppingCart, productId: 1);
                await InsertItemAsync(customer, store.Id + 50, ShoppingCartType.ShoppingCart, productId: 2);
                await InsertItemAsync(customer, store.Id, ShoppingCartType.Wishlist, productId: 2);

                ClearShoppingCartEventConsumer.Reset();
                var result = await CreateController(customer).ClearCart();

                RedirectName(result).Should().Be(NopRouteNames.General.CART);
                (await LoadAsync(customer, ShoppingCartType.ShoppingCart, storeId: 0)).Should().BeEmpty();
                (await LoadAsync(customer, ShoppingCartType.Wishlist, store.Id)).Should().ContainSingle();
                ClearShoppingCartEventConsumer.HandleCount.Should().Be(1);
                ClearShoppingCartEventConsumer.LastCartCount.Should().Be(2);
                (await GetService<ICustomerService>().GetCustomerByIdAsync(customer.Id)).HasShoppingCartItems.Should().BeTrue();
            });
        }
        finally
        {
            await DeleteCustomerCartAsync(customer);
        }
    }

    [Test]
    public async Task ClearCart_WhenSettingIsOff_LeavesCartAndCheckoutDataUntouched()
    {
        var customer = await InsertGuestAsync();
        var store = await GetService<IStoreContext>().GetCurrentStoreAsync();

        try
        {
            await WithSettingsAsync(settings => settings.ShowClearCartButton = false, async () =>
            {
                await InsertItemAsync(customer, store.Id, ShoppingCartType.ShoppingCart);
                await SeedCheckoutDataAsync(customer, store.Id);

                ClearShoppingCartEventConsumer.Reset();
                var result = await CreateController(customer).ClearCart();

                RedirectName(result).Should().Be(NopRouteNames.General.CART);
                (await LoadAsync(customer, ShoppingCartType.ShoppingCart, store.Id)).Should().ContainSingle();
                ClearShoppingCartEventConsumer.HandleCount.Should().Be(0);
                (await GetService<ICustomerService>().ParseAppliedDiscountCouponCodesAsync(customer)).Should().Contain("save10");
            });
        }
        finally
        {
            await DeleteCustomerCartAsync(customer);
        }
    }

    [Test]
    public async Task ClearCart_WhenCartIsEmpty_HasNoSideEffects()
    {
        var customer = await InsertGuestAsync();
        var store = await GetService<IStoreContext>().GetCurrentStoreAsync();

        try
        {
            await SeedCheckoutDataAsync(customer, store.Id);
            ClearShoppingCartEventConsumer.Reset();

            var result = await CreateController(customer).ClearCart();

            RedirectName(result).Should().Be(NopRouteNames.General.CART);
            ClearShoppingCartEventConsumer.HandleCount.Should().Be(0);
            (await GetService<ICustomerService>().ParseAppliedDiscountCouponCodesAsync(customer)).Should().Contain("save10");
            (await GetService<IGenericAttributeService>().GetAttributeAsync<string>(customer, NopCustomerDefaults.CheckoutAttributes, store.Id))
                .Should().Be("<checkout />");
        }
        finally
        {
            await DeleteCustomerCartAsync(customer);
        }
    }

    [Test]
    public async Task ClearCart_WithoutShoppingCartPermission_RedirectsHomeAndDeletesNothing()
    {
        var customer = await InsertGuestAsync();
        var store = await GetService<IStoreContext>().GetCurrentStoreAsync();
        var permissionService = new Mock<IPermissionService>();
        permissionService.Setup(service => service.AuthorizeAsync(StandardPermission.PublicStore.ENABLE_SHOPPING_CART))
            .ReturnsAsync(false);

        try
        {
            await InsertItemAsync(customer, store.Id, ShoppingCartType.ShoppingCart);
            ClearShoppingCartEventConsumer.Reset();

            var result = await CreateController(customer, permissionService.Object).ClearCart();

            RedirectName(result).Should().Be(NopRouteNames.General.HOMEPAGE);
            (await LoadAsync(customer, ShoppingCartType.ShoppingCart, store.Id)).Should().ContainSingle();
            ClearShoppingCartEventConsumer.HandleCount.Should().Be(0);
        }
        finally
        {
            await DeleteCustomerCartAsync(customer);
        }
    }

    [Test]
    public async Task ClearCart_WhenVendorEnabled_ClearsSelectedVendor()
    {
        var customer = await InsertGuestAsync();
        var store = await GetService<IStoreContext>().GetCurrentStoreAsync();

        try
        {
            await WithSettingsAsync(settings => settings.VendorEnabled = true, async () =>
            {
                await InsertItemAsync(customer, store.Id, ShoppingCartType.ShoppingCart);
                await GetService<IGenericAttributeService>().SaveAttributeAsync(customer, NopCustomerDefaults.ShoppingCartVendorAttribute, 7, store.Id);

                var result = await CreateController(customer).ClearCart();

                RedirectName(result).Should().Be(NopRouteNames.General.CART);
                (await LoadAsync(customer, ShoppingCartType.ShoppingCart, store.Id)).Should().BeEmpty();
                (await GetService<IGenericAttributeService>().GetAttributeAsync<int?>(customer, NopCustomerDefaults.ShoppingCartVendorAttribute, store.Id))
                    .Should().BeNull();
            });
        }
        finally
        {
            await DeleteCustomerCartAsync(customer);
        }
    }

    [Test]
    public void ClearCart_IsPostOnlyAndCoveredByAntiforgery()
    {
        typeof(ShoppingCartController).Should().BeDecoratedWith<AutoValidateAntiforgeryTokenAttribute>();

        var method = typeof(ShoppingCartController).GetMethod(nameof(ShoppingCartController.ClearCart));
        method.Should().NotBeNull();
        method.Should().BeDecoratedWith<HttpPostAttribute>();
        method.GetCustomAttributes(typeof(FormValueRequiredAttribute), inherit: true).Should().NotBeEmpty();
        method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true).Should().BeEmpty();
    }

    [Test]
    public async Task ClearCartLocaleResourcesExist()
    {
        var localizationService = GetService<ILocalizationService>();

        (await localizationService.GetResourceAsync("ShoppingCart.ClearCart")).Should().Be("Clear cart");
        (await localizationService.GetResourceAsync("ShoppingCart.ClearCart.Confirm"))
            .Should().Be("Are you sure you want to remove all items from your cart?");
        (await localizationService.GetResourceAsync("Admin.Configuration.Settings.ShoppingCart.ShowClearCartButton"))
            .Should().Be("Show \"Clear cart\" button");
        (await localizationService.GetResourceAsync("Admin.Configuration.Settings.ShoppingCart.ShowClearCartButton.Hint"))
            .Should().Be("Check to show a button on the shopping cart page that removes all items from the cart.");
    }

    private static async Task<Customer> InsertGuestAsync()
    {
        return await GetService<ICustomerService>().InsertGuestCustomerAsync();
    }

    private static async Task<ShoppingCartItem> InsertItemAsync(Customer customer, int storeId, ShoppingCartType type, int productId = 1)
    {
        var item = new ShoppingCartItem
        {
            CustomerId = customer.Id,
            ProductId = productId,
            StoreId = storeId,
            Quantity = 1,
            ShoppingCartType = type,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        await GetService<IRepository<ShoppingCartItem>>().InsertAsync(item);
        ClearCartCache(customer);
        return item;
    }

    private static async Task DeleteCustomerCartAsync(Customer customer)
    {
        var repository = GetService<IRepository<ShoppingCartItem>>();
        var items = await repository.GetAllAsync(query => query.Where(item => item.CustomerId == customer.Id));
        if (items.Any())
            await repository.DeleteAsync(items);

        ClearCartCache(customer);
    }

    private static async Task<IList<ShoppingCartItem>> LoadAsync(Customer customer, ShoppingCartType type, int storeId)
    {
        ClearCartCache(customer);
        return await GetService<IShoppingCartService>().GetShoppingCartAsync(customer, type, storeId);
    }

    private static void ClearCartCache(Customer customer)
    {
        GetService<IShortTermCacheManager>().RemoveByPrefix(NopOrderDefaults.ShoppingCartItemsByCustomerPrefix, customer);
    }

    private static async Task SeedCheckoutDataAsync(Customer customer, int storeId)
    {
        var customerService = GetService<ICustomerService>();
        var attributes = GetService<IGenericAttributeService>();

        await customerService.ApplyDiscountCouponCodeAsync(customer, "SAVE10");
        await customerService.ApplyGiftCardCouponCodeAsync(customer, "GIFT100");
        await attributes.SaveAttributeAsync(customer, NopCustomerDefaults.CheckoutAttributes, "<checkout />", storeId);
        await attributes.SaveAttributeAsync(customer, NopCustomerDefaults.UseRewardPointsDuringCheckoutAttribute, true, storeId);
        await attributes.SaveAttributeAsync(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, new ShippingOption { Name = "Ground", ShippingRateComputationMethodSystemName = "Test" }, storeId);
        await attributes.SaveAttributeAsync(customer, NopCustomerDefaults.OfferedShippingOptionsAttribute, new ShippingOption { Name = "Ground", ShippingRateComputationMethodSystemName = "Test" }, storeId);
        await attributes.SaveAttributeAsync(customer, NopCustomerDefaults.SelectedPickupPointAttribute, new PickupPoint { Id = "1", Name = "Store" }, storeId);
        await attributes.SaveAttributeAsync(customer, NopCustomerDefaults.DesiredDeliveryDate, DateTime.UtcNow.Date.AddDays(2), storeId);
        await attributes.SaveAttributeAsync(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, "Payments.TestMethod", storeId);
    }

    private static async Task AssertCheckoutDataClearedAsync(Customer customer, int storeId)
    {
        var customerService = GetService<ICustomerService>();
        var attributes = GetService<IGenericAttributeService>();

        (await customerService.ParseAppliedDiscountCouponCodesAsync(customer)).Should().BeEmpty();
        (await customerService.ParseAppliedGiftCardCouponCodesAsync(customer)).Should().BeEmpty();
        (await attributes.GetAttributeAsync<string>(customer, NopCustomerDefaults.CheckoutAttributes, storeId)).Should().BeNullOrEmpty();
        (await attributes.GetAttributeAsync<bool>(customer, NopCustomerDefaults.UseRewardPointsDuringCheckoutAttribute, storeId)).Should().BeFalse();
        (await attributes.GetAttributeAsync<ShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, storeId)).Should().BeNull();
        (await attributes.GetAttributeAsync<ShippingOption>(customer, NopCustomerDefaults.OfferedShippingOptionsAttribute, storeId)).Should().BeNull();
        (await attributes.GetAttributeAsync<PickupPoint>(customer, NopCustomerDefaults.SelectedPickupPointAttribute, storeId)).Should().BeNull();
        (await attributes.GetAttributeAsync<DateTime?>(customer, NopCustomerDefaults.DesiredDeliveryDate, storeId)).Should().BeNull();
        (await attributes.GetAttributeAsync<string>(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, storeId)).Should().BeNullOrEmpty();
    }

    private static async Task WithSettingsAsync(Action<ShoppingCartSettings> mutate, Func<Task> action)
    {
        var settingService = GetService<ISettingService>();
        var settings = GetService<ShoppingCartSettings>();
        var showClearCartButton = settings.ShowClearCartButton;
        var cartsShared = settings.CartsSharedBetweenStores;
        var vendorEnabled = settings.VendorEnabled;

        mutate(settings);
        await settingService.SaveSettingAsync(settings);

        try
        {
            await action();
        }
        finally
        {
            settings = GetService<ShoppingCartSettings>();
            settings.ShowClearCartButton = showClearCartButton;
            settings.CartsSharedBetweenStores = cartsShared;
            settings.VendorEnabled = vendorEnabled;
            await settingService.SaveSettingAsync(settings);
        }
    }

    private static ShoppingCartController CreateController(Customer customer, IPermissionService permissionService = null)
    {
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(customer);

        return new ShoppingCartController(
            GetService<CaptchaSettings>(),
            GetService<CustomerSettings>(),
            GetService<IAttributeParser<CheckoutAttribute, CheckoutAttributeValue>>(),
            GetService<IAttributeService<CheckoutAttribute, CheckoutAttributeValue>>(),
            GetService<ICurrencyService>(),
            GetService<ICustomerActivityService>(),
            GetService<ICustomerService>(),
            GetService<ICustomWishlistService>(),
            GetService<IDiscountService>(),
            GetService<IDownloadService>(),
            GetService<IGenericAttributeService>(),
            GetService<IGiftCardService>(),
            GetService<IHtmlFormatter>(),
            GetService<ILocalizationService>(),
            GetService<INopFileProvider>(),
            GetService<INopUrlHelper>(),
            GetService<INotificationService>(),
            permissionService ?? GetService<IPermissionService>(),
            GetService<IPictureService>(),
            GetService<IPriceFormatter>(),
            GetService<IProductAttributeParser>(),
            GetService<IProductAttributeService>(),
            GetService<IProductService>(),
            GetService<IShippingService>(),
            GetService<IShoppingCartModelFactory>(),
            GetService<IShoppingCartService>(),
            GetService<IStaticCacheManager>(),
            GetService<IStoreContext>(),
            GetService<IStoreMappingService>(),
            GetService<ITaxService>(),
            GetService<IWebHelper>(),
            workContext.Object,
            GetService<IWorkflowMessageService>(),
            GetService<MediaSettings>(),
            GetService<OrderSettings>(),
            GetService<ShoppingCartSettings>(),
            GetService<ShippingSettings>());
    }

    private static string RedirectName(IActionResult result)
    {
        return result.Should().BeOfType<RedirectToRouteResult>().Subject.RouteName;
    }

    public class ClearShoppingCartEventConsumer : IConsumer<ClearShoppingCartEvent>
    {
        public static int HandleCount { get; private set; }

        public static int LastCartCount { get; private set; }

        public static void Reset()
        {
            HandleCount = 0;
            LastCartCount = 0;
        }

        public Task HandleEventAsync(ClearShoppingCartEvent eventMessage)
        {
            HandleCount++;
            LastCartCount = eventMessage.Cart?.Count ?? 0;
            return Task.CompletedTask;
        }
    }
}
