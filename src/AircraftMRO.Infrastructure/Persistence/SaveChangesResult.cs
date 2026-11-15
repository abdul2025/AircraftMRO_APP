using AircraftMRO.Domain.Common.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AircraftMRO.Infrastructure.Persistence;

/// <summary>
/// Saves and turns the expected rejections into a failed <see cref="Result"/>: a stale row version
/// becomes the concurrency error and a unique index violation the error mapped to that index.
/// Any other failure stays an exception.
/// </summary>
internal static class SaveChangesResult
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public static async Task<Result> SaveAsync(
        DbContext dbContext,
        (string Code, string Message) concurrencyError,
        IReadOnlyDictionary<string, (string Code, string Message)> uniqueIndexErrors,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(concurrencyError.Code, concurrencyError.Message);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql)
        {
            foreach (var (indexName, error) in uniqueIndexErrors)
            {
                if (sql.Message.Contains(indexName, StringComparison.Ordinal))
                {
                    return Result.Failure(error.Code, error.Message);
                }
            }

            throw;
        }
    }
}
