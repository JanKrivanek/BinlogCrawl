namespace BinlogCrawl;

class Program
{
    static void Main(string[] args)
    {
        string binlogPath = args.Length > 0 ?args[0] : @"msbuild.binlog";
        BinlogCrawler.Crawl(binlogPath);
    }
}
