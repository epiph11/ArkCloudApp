using ArkCloud.Domain.Entities;
using ArkCloud.Domain.Enums;
using ArkCloud.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace ArkCloud.Tests.Unit.Domain;

public class OrderTests
{
    [Fact]
    public void Should_Add_Item_To_Draft_Order()
    {
        var order = Order.Create(Guid.NewGuid());

        order.AddItem(Guid.NewGuid(), 2, 10);

        order.Items.Should().HaveCount(1);
        order.TotalAmount.Should().Be(20);
    }

    [Fact]
    public void Should_Not_Submit_Empty_Order()
    {
        var order = Order.Create(Guid.NewGuid());

        var action = () => order.Submit();

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void Should_Submit_Valid_Order()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 1, 100);

        order.Submit();

        order.Status.Should().Be(OrderStatus.Submitted);
    }

    [Fact]
    public void Should_Not_Add_Item_To_Non_Draft_Order()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 1, 10);
        order.Submit();

        var action = () => order.AddItem(Guid.NewGuid(), 1, 10);

        action.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void Should_Mark_Submitted_Order_As_Paid()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 1, 10);
        order.Submit();

        order.MarkAsPaid();

        order.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public void Should_Not_Mark_Draft_Order_As_Paid()
    {
        var order = Order.Create(Guid.NewGuid());

        var action = () => order.MarkAsPaid();

        action.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void Should_Cancel_Draft_Order()
    {
        var order = Order.Create(Guid.NewGuid());

        order.Cancel();

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Should_Not_Cancel_Paid_Order()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 1, 10);
        order.Submit();
        order.MarkAsPaid();

        var action = () => order.Cancel();

        action.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void Should_Compute_Total_Amount_Across_Multiple_Items()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), 2, 10);
        order.AddItem(Guid.NewGuid(), 3, 5);

        order.TotalAmount.Should().Be(35);
    }
}
