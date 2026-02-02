namespace SobeSobe.Api.DTOs;

public class ListGamesResponse
{
    public required List<GameListItem> Games { get; set; }
    public required PaginationInfo Pagination { get; set; }
}

public class GameListItem
{
    public required Guid Id { get; set; }
    public required UserSummary CreatedBy { get; set; }
    public required int Status { get; set; }
    public required int MaxPlayers { get; set; }
    public required int CurrentPlayers { get; set; }
    public required List<PlayerSummary> Players { get; set; }
    public required DateTime CreatedAt { get; set; }
}

public class UserSummary
{
    public required Guid Id { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
}

public class PlayerSummary
{
    public required Guid UserId { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required int Position { get; set; }
}

public class PaginationInfo
{
    public required int Page { get; set; }
    public required int PageSize { get; set; }
    public required int TotalPages { get; set; }
    public required int TotalItems { get; set; }
}
