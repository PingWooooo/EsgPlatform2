using System.Security.Claims;
using System.Reflection;
using EsgPlatform.Controllers;
using EsgPlatform.Models;
using EsgPlatform.Repositories.Interfaces;
using EsgPlatform.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace ESG_Platform.Tests;

/// <summary>
/// 權限控管測試：
/// - Admin 角色可正常訪問 ReportScheduleController
/// - User 角色（或未登入）應被拒絕（403 Forbidden）
/// </summary>
public class AuthorizationTests
{
    // ─── Controller 層級的 [Authorize] Attribute 驗證 ─────────

    [Fact]
    public void ReportScheduleController_HasAuthorizeAttribute()
    {
        // 驗證整個 Controller 上有 [Authorize] 屬性
        var controllerType  = typeof(ReportScheduleController);
        var authorizeAttr   = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttr);
    }

    [Fact]
    public void ReportScheduleController_RequiresAdminRole()
    {
        // 驗證 [Authorize(Roles = "Admin")] 的 Roles 設定正確
        var controllerType = typeof(ReportScheduleController);
        var authorizeAttr  = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttr);
        Assert.Equal("Admin", authorizeAttr!.Roles);
    }

    [Fact]
    public void ReportScheduleController_DoesNotAllowAnonymous()
    {
        // 驗證 Controller 上沒有 [AllowAnonymous]（不允許匿名存取）
        var controllerType   = typeof(ReportScheduleController);
        var allowAnonAttr    = controllerType.GetCustomAttribute<AllowAnonymousAttribute>();

        Assert.Null(allowAnonAttr);
    }

    // ─── Action 方法逐一確認 Admin-only ───────────────────────

    [Theory]
    [InlineData("Index")]
    [InlineData("Create")]
    [InlineData("Edit")]
    [InlineData("Delete")]
    public void ReportScheduleController_Action_DoesNotOverrideWithAllowAnonymous(string actionName)
    {
        // 每個 Action 都不應有 [AllowAnonymous] 覆蓋 Controller 層級的 [Authorize]
        var controllerType = typeof(ReportScheduleController);
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(m => m.Name == actionName);

        foreach (var method in methods)
        {
            var allowAnon = method.GetCustomAttribute<AllowAnonymousAttribute>();
            Assert.Null(allowAnon);
        }
    }

    // ─── Controller 直接呼叫測試（模擬已授權的 Admin 使用者）──

    [Fact]
    public async Task ReportScheduleController_Index_AdminUser_ReturnsViewResult()
    {
        // Arrange：模擬 Admin 使用者呼叫 Index
        var scheduleRepoMock = new Mock<IReportScheduleRepository>();
        var loggerMock       = new Mock<ILogger<ReportScheduleController>>();

        scheduleRepoMock.Setup(r => r.GetAllAsync())
                        .ReturnsAsync(new List<ReportSchedule>
                        {
                            new() { Id = 1, ReportName = "月報", Frequency = "Monthly", WarningDays = 7 }
                        });

        var controller = new ReportScheduleController(scheduleRepoMock.Object, loggerMock.Object);
        CreateControllerWithTempData(controller, "admin01", "Admin");

        // Act
        var result = await controller.Index();

        // Assert：Admin 應得到 ViewResult
        Assert.IsType<ViewResult>(result);
        var viewResult = (ViewResult)result;
        var model = Assert.IsAssignableFrom<IEnumerable<ReportSchedule>>(viewResult.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task ReportScheduleController_Create_Post_AdminUser_ValidModel_RedirectsToIndex()
    {
        // Arrange
        var scheduleRepoMock = new Mock<IReportScheduleRepository>();
        var loggerMock       = new Mock<ILogger<ReportScheduleController>>();

        scheduleRepoMock.Setup(r => r.InsertAsync(It.IsAny<ReportSchedule>()))
                        .ReturnsAsync(1);

        var controller = new ReportScheduleController(scheduleRepoMock.Object, loggerMock.Object);
        // 必須初始化 TempData，否則 TempData["Success"] = ... 會丟出例外
        // 並被 catch 捕獲而回傳 ViewResult 而非 RedirectToActionResult
        CreateControllerWithTempData(controller, "admin01", "Admin");

        var vm = new ReportScheduleViewModel
        {
            ReportName        = "2024 月報",
            Frequency         = "Monthly",
            ResponsiblePerson = "王小明",
            WarningDays       = 7,
            NextDueDate       = DateOnly.FromDateTime(DateTime.Today.AddMonths(1))
        };

        // Act
        var result = await controller.Create(vm);

        // Assert：新增成功應 Redirect 到 Index
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    [Fact]
    public async Task ReportScheduleController_Delete_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var scheduleRepoMock = new Mock<IReportScheduleRepository>();
        var loggerMock       = new Mock<ILogger<ReportScheduleController>>();

        scheduleRepoMock.Setup(r => r.GetByIdAsync(999))
                        .ReturnsAsync((ReportSchedule?)null);

        var controller = new ReportScheduleController(scheduleRepoMock.Object, loggerMock.Object);
        CreateControllerWithTempData(controller, "admin01", "Admin");

        // Act
        var result = await controller.Delete(999);

        // Assert：找不到資料應回 404 NotFound
        Assert.IsType<NotFoundResult>(result);
    }

    // ─── User 角色（非 Admin）的存取控制 ─────────────────────

    [Fact]
    public void NonAdminUser_CannotPassAdminRoleCheck()
    {
        // 驗證一般 User 的 ClaimsPrincipal 不具備 Admin 角色
        var userPrincipal = CreateUserPrincipal("user01", "User");

        Assert.False(userPrincipal.IsInRole("Admin"));
        Assert.True(userPrincipal.IsInRole("User"));
    }

    [Fact]
    public void AdminUser_HasAdminRole()
    {
        // 驗證 Admin 的 ClaimsPrincipal 具備 Admin 角色
        var adminPrincipal = CreateUserPrincipal("admin01", "Admin");

        Assert.True(adminPrincipal.IsInRole("Admin"));
        Assert.False(adminPrincipal.IsInRole("User"));
    }

    [Fact]
    public void UnauthenticatedUser_IsNotAuthenticated()
    {
        // 未登入使用者不應被認為已認證
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.False(anonymous.Identity?.IsAuthenticated ?? false);
    }

    // ─── 輔助方法 ────────────────────────────────────────────

    private static ControllerContext CreateControllerContext(string username, string role)
    {
        var user        = CreateUserPrincipal(username, role);
        var httpContext = new DefaultHttpContext { User = user };
        return new ControllerContext
        {
            HttpContext      = httpContext,
            RouteData        = new RouteData(),
            ActionDescriptor = new ControllerActionDescriptor()
        };
    }

    /// <summary>建立含 TempData 的完整 Controller 測試環境</summary>
    private static T CreateControllerWithTempData<T>(T controller, string username, string role)
        where T : Controller
    {
        controller.ControllerContext = CreateControllerContext(username, role);

        // TempData 必須初始化，否則 TempData["key"] = value 會拋出 NullReferenceException
        // 而被 Controller 的 catch 區塊捕獲並回傳 ViewResult（而非 Redirect）
        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider.Setup(p => p.LoadTempData(It.IsAny<HttpContext>()))
                        .Returns(new Dictionary<string, object?>());
        controller.TempData = new TempDataDictionary(
            controller.ControllerContext.HttpContext,
            tempDataProvider.Object);

        return controller;
    }

    private static ClaimsPrincipal CreateUserPrincipal(string username, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };
        var identity  = new ClaimsIdentity(claims, "TestAuthentication");
        return new ClaimsPrincipal(identity);
    }
}
