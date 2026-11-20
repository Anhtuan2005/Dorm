using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.ViewModels;
using Xunit;

namespace DormitoryManagementSystem.Tests;

public sealed class RoomAmenityInputTests
{
    [Fact]
    public void Editing_legacy_amenities_preserves_custom_names_with_commas()
    {
        const string legacy = "WC khép kín|Quạt|Kệ sách, loại treo";
        var edit = new UpdateRoomInput { SelectedAmenities = ["Quạt trần", "Quạt đứng"] };
        Assert.Equal("WC khép kín|Quạt trần|Quạt đứng|Kệ sách, loại treo", edit.GetStoredAmenities(legacy));
        Assert.Equal("WC khép kín|Kệ sách, loại treo", new UpdateRoomInput().GetStoredAmenities(legacy));
        Assert.Equal("WC khép kín", new CreateRoomInput().GetStoredAmenities());
    }

    [Fact]
    public void Custom_entries_cannot_be_added_through_checkboxes_or_older_forms()
    {
        const string unsupported = "Kệ bên máy lạnh";
        CreateRoomInput[] inputs = [
            new() { SelectedAmenities = [unsupported] },
            new() { OtherAmenities = unsupported },
            new() { Amenities = unsupported }
        ];
        foreach (var input in inputs)
        {
            Assert.Null(input.GetStoredAmenities());
            Assert.Null(input.GetStoredAmenities("WC khép kín|Kệ sách"));
        }
        Assert.Null(RoomAmenities.ModelKey(unsupported));
    }

    [Fact]
    public void Known_legacy_names_resolve_to_the_corresponding_checkboxes()
    {
        Assert.Equal(new[] { "Điều hòa", "Quạt trần", "Quạt đứng", "Tủ lạnh" },
            RoomAmenities.Optional("May lanh|Quạt|Quạt trần|Tu lanh"));
    }

    [Fact]
    public void Older_forms_can_still_post_a_single_amenities_field()
    {
        var input = new CreateRoomInput { Amenities = "Wi-Fi|Bàn học" };
        Assert.Equal("WC khép kín|Wi-Fi|Bàn học", input.GetStoredAmenities());
    }
}
