namespace DatPlotX.Content;

public abstract record HelpBlock;

public record ParagraphBlock(string Text) : HelpBlock;

public record SubHeadingBlock(string Text) : HelpBlock;

public record BulletListBlock(List<string> Items) : HelpBlock;

public record TableBlock(string FormattedText) : HelpBlock;

public record HelpSection(string Title, List<HelpBlock> Blocks);
