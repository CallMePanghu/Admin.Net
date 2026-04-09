// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// SqlSugar 实体仓储
/// </summary>
/// <typeparam name="T"></typeparam>
public class SqlSugarRepository<T> : SimpleClient<T>, ISqlSugarRepository<T> where T : class, new()
{
    public SqlSugarRepository()
    {
        var iTenant = SqlSugarSetup.ITenant; // App.GetRequiredService<ISqlSugarClient>().AsTenant();
        var tenantType = typeof(T);

        // 1. 检查是否需要忽略隔离
        var ignoreAttr = tenantType.GetCustomAttribute<TenantIsolatedIgnoreAttribute>();
        if (ignoreAttr != null)
        {
            // 必须在主数据库的实体
            base.Context = iTenant.GetConnectionScope(SqlSugarConst.MainConfigId);
            return;
        }

        // 2. 检查多库特性
        if (tenantType.IsDefined(typeof(TenantAttribute), false))
        {
            base.Context = iTenant.GetConnectionScopeWithAttr<T>();
            return;
        }

        // 3. 检查日志表特性
        if (tenantType.IsDefined(typeof(LogTableAttribute), false))
        {
            if (iTenant.IsAnyConnection(SqlSugarConst.LogConfigId))
                base.Context = iTenant.GetConnectionScope(SqlSugarConst.LogConfigId);
            return;
        }

        // 4. 检查系统表特性 - 根据租户类型决定是否隔离
        if (tenantType.IsDefined(typeof(SysTableAttribute), false))
        {
            // 获取当前租户信息
            var tenantId = App.HttpContext?.Request.Headers[ClaimConst.TenantId].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = App.User?.FindFirst(ClaimConst.TenantId)?.Value;
            }

            if (!string.IsNullOrWhiteSpace(tenantId) && tenantId != SqlSugarConst.MainConfigId)
            {
                var tenant = App.GetRequiredService<SysTenantService>().GetTenant(long.Parse(tenantId)).GetAwaiter().GetResult();
                if (tenant != null && tenant.TenantType == TenantTypeEnum.Db)
                {
                    // 数据库隔离租户，使用租户数据库
                    var tenantDb = App.GetRequiredService<SysTenantService>().GetTenantDbConnectionScope(long.Parse(tenantId));
                    if (tenantDb != null)
                    {
                        base.Context = tenantDb;
                        return;
                    }
                }
            }

            // 默认使用主数据库
            base.Context = iTenant.GetConnectionScope(SqlSugarConst.MainConfigId);
            return;
        }

        // 5. 默认处理逻辑 - 根据租户ID切换数据库
        var defaultTenantId = App.HttpContext?.Request.Headers[ClaimConst.TenantId].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(defaultTenantId))
        {
            defaultTenantId = App.User?.FindFirst(ClaimConst.TenantId)?.Value;
        }

        if (!string.IsNullOrWhiteSpace(defaultTenantId) && defaultTenantId != SqlSugarConst.MainConfigId)
        {
            var tenantDb = App.GetRequiredService<SysTenantService>().GetTenantDbConnectionScope(long.Parse(defaultTenantId));
            if (tenantDb != null)
            {
                base.Context = tenantDb;
                return;
            }
        }

        base.Context = iTenant.GetConnectionScope(SqlSugarConst.MainConfigId);
    }

    #region 分表操作

    public async Task<bool> SplitTableInsertAsync(T input)
    {
        return await base.AsInsertable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public async Task<bool> SplitTableInsertAsync(List<T> input)
    {
        return await base.AsInsertable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public async Task<bool> SplitTableUpdateAsync(T input)
    {
        return await base.AsUpdateable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public async Task<bool> SplitTableUpdateAsync(List<T> input)
    {
        return await base.AsUpdateable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public async Task<bool> SplitTableDeleteableAsync(T input)
    {
        return await base.Context.Deleteable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public async Task<bool> SplitTableDeleteableAsync(List<T> input)
    {
        return await base.Context.Deleteable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public Task<T> SplitTableGetFirstAsync(Expression<Func<T, bool>> whereExpression)
    {
        return base.AsQueryable().SplitTable().FirstAsync(whereExpression);
    }

    public Task<bool> SplitTableIsAnyAsync(Expression<Func<T, bool>> whereExpression)
    {
        return base.Context.Queryable<T>().Where(whereExpression).SplitTable().AnyAsync();
    }

    public Task<List<T>> SplitTableGetListAsync()
    {
        return Context.Queryable<T>().SplitTable().ToListAsync();
    }

    public Task<List<T>> SplitTableGetListAsync(Expression<Func<T, bool>> whereExpression)
    {
        return Context.Queryable<T>().Where(whereExpression).SplitTable().ToListAsync();
    }

    public Task<List<T>> SplitTableGetListAsync(Expression<Func<T, bool>> whereExpression, string[] tableNames)
    {
        return Context.Queryable<T>().Where(whereExpression).SplitTable(t => t.InTableNames(tableNames)).ToListAsync();
    }

    #endregion 分表操作
}