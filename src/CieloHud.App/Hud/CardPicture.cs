using CieloHud.Core.Cards;

namespace CieloHud.App.Hud;

/// <summary>What the drawing on a card shows, worked out in Core for this moment (decision 041). The ISS has none.</summary>
public abstract record CardPicture;

/// <summary>The Moon, Mercury, Venus or Mars: the lit part of the disc, turned as in the sky.</summary>
public sealed record PhasePicture(BodyDisc Disc) : CardPicture;

/// <summary>Jupiter as a dot, with the moons that can be told apart from it in their places.</summary>
public sealed record JupiterPicture(JupiterMoonsFacts Moons) : CardPicture;

/// <summary>Saturn's globe and rings, with today's tilt.</summary>
public sealed record SaturnPicture(SaturnShape Shape) : CardPicture;

/// <summary>A star: a point of light in its color, which is all the eye sees.</summary>
public sealed record StarPicture(StarColor Color) : CardPicture;

/// <summary>
/// A constellation as it looks now: its figure and its stars (decision 049), with the names of the brightest by designation, in Spanish.
/// </summary>
public sealed record ConstellationPicture(ConstellationShape Shape, IReadOnlyDictionary<string, string> Labels) : CardPicture;
