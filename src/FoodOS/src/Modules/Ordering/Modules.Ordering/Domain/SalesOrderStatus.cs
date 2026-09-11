using System.Net;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Ordering.Domain;

public enum SalesOrderStatus
{
    Draft = 0,
    Reserved = 1,
    Planned = 2,
    Picking = 3,
    Packed = 4,
    InTransit = 5,
    Received = 6,
    Reconciled = 7,
    Cancelled = 8
}

public static class SalesOrderTransitions
{
    public static bool CanTransition(SalesOrderStatus from, SalesOrderStatus to) => (from, to) switch
    {
        (SalesOrderStatus.Draft, SalesOrderStatus.Reserved) => true,
        (SalesOrderStatus.Reserved, SalesOrderStatus.Reserved) => true,
        (SalesOrderStatus.Reserved, SalesOrderStatus.Cancelled) => true,
        (SalesOrderStatus.Reserved, SalesOrderStatus.Planned) => true,
        (SalesOrderStatus.Planned, SalesOrderStatus.Picking) => true,
        (SalesOrderStatus.Picking, SalesOrderStatus.Packed) => true,
        (SalesOrderStatus.Packed, SalesOrderStatus.InTransit) => true,
        (SalesOrderStatus.InTransit, SalesOrderStatus.Received) => true,
        (SalesOrderStatus.Received, SalesOrderStatus.Reconciled) => true,
        _ => false
    };

    public static void Ensure(SalesOrderStatus from, SalesOrderStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new CustomException(
                $"Cannot transition order from {from} to {to}.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }
    }
}
