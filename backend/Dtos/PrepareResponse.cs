namespace Voixla.Api.Dtos;

public sealed record PrepareResponse(string Voice, IReadOnlyList<ChunkDto> Chunks);
