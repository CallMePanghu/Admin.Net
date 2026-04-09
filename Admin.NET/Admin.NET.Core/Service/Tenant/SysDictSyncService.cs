// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

using Mapster;

/// <summary>
/// 字典同步服务
/// </summary>
[ApiDescriptionSettings(Order = 392)]
public class SysDictSyncService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<SysDictType> _sysDictTypeRep;
    private readonly SqlSugarRepository<SysDictData> _sysDictDataRep;
    private readonly SysTenantService _sysTenantService;

    public SysDictSyncService(
        SqlSugarRepository<SysDictType> sysDictTypeRep,
        SqlSugarRepository<SysDictData> sysDictDataRep,
        SysTenantService sysTenantService)
    {
        _sysDictTypeRep = sysDictTypeRep;
        _sysDictDataRep = sysDictDataRep;
        _sysTenantService = sysTenantService;
    }

    /// <summary>
    /// 同步字典类型到租户数据库
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    public async Task SyncDictTypesToTenant(long tenantId)
    {
        var tenant = await _sysTenantService.GetTenant(tenantId);
        if (tenant == null || tenant.TenantType != TenantTypeEnum.Db)
            return;

        var tenantDb = _sysTenantService.GetTenantDbConnectionScope(tenantId);
        if (tenantDb == null) return;

        // 获取主数据库的字典类型
        var dictTypes = await _sysDictTypeRep.AsQueryable().ToListAsync();

        // 同步到租户数据库
        foreach (var dictType in dictTypes)
        {
            var dictTypeCopy = dictType.Adapt<SysDictType>();
            dictTypeCopy.Id = 0; // 重置ID
            dictTypeCopy.TenantId = null;

            await tenantDb.Insertable(dictTypeCopy).ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 同步字典数据到租户数据库
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    public async Task SyncDictDataToTenant(long tenantId)
    {
        var tenant = await _sysTenantService.GetTenant(tenantId);
        if (tenant == null || tenant.TenantType != TenantTypeEnum.Db)
            return;

        var tenantDb = _sysTenantService.GetTenantDbConnectionScope(tenantId);
        if (tenantDb == null) return;

        // 获取主数据库的字典数据
        var dictData = await _sysDictDataRep.AsQueryable().ToListAsync();

        // 同步到租户数据库
        foreach (var data in dictData)
        {
            var dataCopy = data.Adapt<SysDictData>();
            dataCopy.Id = 0; // 重置ID
            dataCopy.TenantId = null;

            await tenantDb.Insertable(dataCopy).ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 同步所有字典数据到租户数据库
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    public async Task SyncAllDictsToTenant(long tenantId)
    {
        await SyncDictTypesToTenant(tenantId);
        await SyncDictDataToTenant(tenantId);
    }
}