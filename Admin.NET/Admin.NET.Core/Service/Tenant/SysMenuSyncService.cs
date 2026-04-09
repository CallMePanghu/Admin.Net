// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 菜单同步服务
/// </summary>
[ApiDescriptionSettings(Order = 391)]
public class SysMenuSyncService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<SysMenu> _sysMenuRep;
    private readonly SqlSugarRepository<SysTenantMenu> _sysTenantMenuRep;
    private readonly SysTenantService _sysTenantService;

    public SysMenuSyncService(
        SqlSugarRepository<SysMenu> sysMenuRep,
        SqlSugarRepository<SysTenantMenu> sysTenantMenuRep,
        SysTenantService sysTenantService)
    {
        _sysMenuRep = sysMenuRep;
        _sysTenantMenuRep = sysTenantMenuRep;
        _sysTenantService = sysTenantService;
    }

    /// <summary>
    /// 同步菜单模板到租户数据库
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    public async Task SyncMenuTemplateToTenant(long tenantId)
    {
        var tenant = await _sysTenantService.GetTenant(tenantId);
        if (tenant == null || tenant.TenantType != TenantTypeEnum.Db)
            return;

        // 获取主数据库的菜单模板
        var menuTemplate = await _sysMenuRep.AsQueryable()
            .Where(m => m.Pid == 0) // 获取顶级菜单
            .ToListAsync();

        // 创建租户数据库的菜单副本
        var tenantDb = _sysTenantService.GetTenantDbConnectionScope(tenantId);
        if (tenantDb == null) return;

        // 同步菜单结构
        await SyncMenuStructure(menuTemplate, tenantDb);
    }

    /// <summary>
    /// 同步菜单到租户数据库
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="menuIdList">菜单ID列表</param>
    public async Task SyncMenuOnGrant(long tenantId, List<long> menuIdList)
    {
        var tenantDb = _sysTenantService.GetTenantDbConnectionScope(tenantId);
        if (tenantDb == null) return;

        // 获取需要同步的菜单
        var menusToSync = await _sysMenuRep.AsQueryable()
            .Where(m => menuIdList.Contains(m.Id))
            .ToListAsync();

        // 同步到租户数据库
        await SyncMenuStructure(menusToSync, tenantDb);
    }

    /// <summary>
    /// 同步菜单结构到指定数据库
    /// </summary>
    /// <param name="menus">菜单列表</param>
    /// <param name="db">目标数据库连接</param>
    private async Task SyncMenuStructure(List<SysMenu> menus, SqlSugarScopeProvider db)
    {
        foreach (var menu in menus)
        {
            // 创建菜单副本
            var menuCopy = menu.Adapt<SysMenu>();
            menuCopy.Id = 0; // 重置ID
            menuCopy.TenantId = null; // 清除租户ID

            // 插入到目标数据库
            await db.Insertable(menuCopy).ExecuteCommandAsync();

            // 递归同步子菜单
            if (menu.Children?.Any() == true)
            {
                await SyncMenuStructure(menu.Children.ToList(), db);
            }
        }
    }

    /// <summary>
    /// 同步所有菜单到租户数据库
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    public async Task SyncAllMenusToTenant(long tenantId)
    {
        var tenantDb = _sysTenantService.GetTenantDbConnectionScope(tenantId);
        if (tenantDb == null) return;

        // 获取主数据库的所有菜单
        var allMenus = await _sysMenuRep.AsQueryable().ToListAsync();

        // 同步到租户数据库
        await SyncMenuStructure(allMenus, tenantDb);
    }
}