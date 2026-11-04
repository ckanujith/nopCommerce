using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Vendors;
using Nop.Services.Discounts;
using Nop.Services.ExportImport;
using Nop.Services.ExportImport.Help;
using Nop.Services.Localization;
using Nop.Services.Logging;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.ExportImport;

[TestFixture]
public class ImportManagerDiscountTests
{
    [Test]
    public async Task ImportKeepsDiscountStartAndEndOnTheMatchingProperties()
    {
        var start = new DateTime(2026, 3, 1, 8, 0, 0);
        var end = new DateTime(2026, 3, 31, 18, 30, 0);
        var openEndedStart = new DateTime(2026, 4, 1, 0, 0, 0);

        var existing = new Discount
        {
            Id = 7,
            Name = "Spring sale",
            StartDateUtc = new DateTime(2020, 1, 1),
            EndDateUtc = new DateTime(2020, 1, 31)
        };

        var stored = new Dictionary<int, Discount> { [existing.Id] = existing };
        var inserted = new List<Discount>();

        var discountService = new Mock<IDiscountService>();
        discountService
            .Setup(service => service.GetDiscountByIdAsync(It.IsAny<int>()))
            .Returns<int>(id => Task.FromResult(stored.GetValueOrDefault(id)));
        discountService
            .Setup(service => service.InsertDiscountAsync(It.IsAny<Discount>()))
            .Callback<Discount>(inserted.Add)
            .Returns(Task.CompletedTask);
        discountService
            .Setup(service => service.UpdateDiscountAsync(It.IsAny<Discount>()))
            .Returns(Task.CompletedTask);

        var languageService = new Mock<ILanguageService>();
        languageService
            .Setup(service => service.GetAllLanguagesAsync(true, 0))
            .ReturnsAsync(new List<Language>());

        var localizationService = new Mock<ILocalizationService>();
        localizationService
            .Setup(service => service.GetResourceAsync("ActivityLog.ImportDiscounts"))
            .ReturnsAsync("{0} discounts were imported");

        var customerActivityService = new Mock<ICustomerActivityService>();
        customerActivityService
            .Setup(service => service.InsertActivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BaseEntity>()))
            .ReturnsAsync((ActivityLog)null);

        var workContext = new Mock<IWorkContext>();
        workContext
            .Setup(context => context.GetCurrentVendorAsync())
            .ReturnsAsync((Vendor)null);

        var importManager = CreateImportManager(
            discountService.Object,
            languageService.Object,
            localizationService.Object,
            customerActivityService.Object,
            workContext.Object);

        var workbook = await new PropertyManager<Discount>([
            new PropertyByName<Discount>("Id", (discount, _) => discount.Id),
            new PropertyByName<Discount>("Name", (discount, _) => discount.Name),
            new PropertyByName<Discount>("StartDateUtc", (discount, _) => discount.StartDateUtc),
            new PropertyByName<Discount>("EndDateUtc", (discount, _) => discount.EndDateUtc)
        ], new CatalogSettings()).ExportToXlsxAsync([
            new Discount
            {
                Id = existing.Id,
                Name = existing.Name,
                StartDateUtc = start,
                EndDateUtc = end
            },
            new Discount
            {
                Name = "Open ended",
                StartDateUtc = openEndedStart
            }
        ]);

        await using var stream = new MemoryStream(workbook);
        await importManager.ImportDiscountsFromXlsxAsync(stream);

        existing.StartDateUtc.Should().Be(start);
        existing.EndDateUtc.Should().Be(end);

        inserted.Should().ContainSingle();
        inserted[0].Name.Should().Be("Open ended");
        inserted[0].StartDateUtc.Should().Be(openEndedStart);
        inserted[0].EndDateUtc.Should().BeNull();
    }

    private static ImportManager CreateImportManager(
        IDiscountService discountService,
        ILanguageService languageService,
        ILocalizationService localizationService,
        ICustomerActivityService customerActivityService,
        IWorkContext workContext)
    {
        return new ImportManager(
            new CatalogSettings(),
            addressService: null,
            backInStockSubscriptionService: null,
            categoryService: null,
            countryService: null,
            customerActivityService,
            customerService: null,
            customNumberFormatter: null,
            dataProvider: null,
            dateRangeService: null,
            discountService,
            filterLevelValueService: null,
            genericAttributeService: null,
            httpClientFactory: null,
            languageService,
            localizationService,
            localizedEntityService: null,
            logger: null,
            manufacturerService: null,
            measureService: null,
            newsLetterSubscriptionService: null,
            newsLetterSubscriptionTypeService: null,
            fileProvider: null,
            orderService: null,
            pictureService: null,
            priceListService: null,
            productAttributeService: null,
            productService: null,
            productTagService: null,
            productTemplateService: null,
            serviceScopeFactory: null,
            specificationAttributeService: null,
            stateProvinceService: null,
            storeContext: null,
            storeMappingService: null,
            storeService: null,
            taxCategoryService: null,
            urlRecordService: null,
            vendorService: null,
            warehouseService: null,
            workContext,
            mediaSettings: null,
            securitySettings: null,
            taxSettings: null,
            vendorSettings: null);
    }
}
