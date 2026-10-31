using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpxNames
    {
    internal static readonly XNamespace Ha = "http://www.hancom.co.kr/hwpml/2011/app";
    internal static readonly XNamespace Hp = "http://www.hancom.co.kr/hwpml/2011/paragraph";
    internal static readonly XNamespace Hp10 = "http://www.hancom.co.kr/hwpml/2016/paragraph";
    internal static readonly XNamespace Hs = "http://www.hancom.co.kr/hwpml/2011/section";
    internal static readonly XNamespace Hc = "http://www.hancom.co.kr/hwpml/2011/core";
    internal static readonly XNamespace Hh = "http://www.hancom.co.kr/hwpml/2011/head";
    internal static readonly XNamespace Hhs = "http://www.hancom.co.kr/hwpml/2011/history";
    internal static readonly XNamespace Hm = "http://www.hancom.co.kr/hwpml/2011/master-page";
    internal static readonly XNamespace Hpf = "http://www.hancom.co.kr/schema/2011/hpf";
    internal static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    internal static readonly XNamespace Opf = "http://www.idpf.org/2007/opf/";
    internal static readonly XNamespace Ocf = "urn:oasis:names:tc:opendocument:xmlns:container";
    internal static readonly XNamespace Odf = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
    internal static readonly XNamespace Hv = "http://www.hancom.co.kr/hwpml/2011/version";
    internal static readonly XNamespace Config = "urn:oasis:names:tc:opendocument:xmlns:config:1.0";
    internal static readonly XNamespace UnitChar = "http://www.hancom.co.kr/hwpml/2016/HwpUnitChar";

    internal static object[] Namespaces() => new object[]
    {
        new XAttribute(XNamespace.Xmlns + "ha", Ha),
        new XAttribute(XNamespace.Xmlns + "hp", Hp),
        new XAttribute(XNamespace.Xmlns + "hp10", Hp10),
        new XAttribute(XNamespace.Xmlns + "hs", Hs),
        new XAttribute(XNamespace.Xmlns + "hc", Hc),
        new XAttribute(XNamespace.Xmlns + "hh", Hh),
        new XAttribute(XNamespace.Xmlns + "hhs", Hhs),
        new XAttribute(XNamespace.Xmlns + "hm", Hm),
        new XAttribute(XNamespace.Xmlns + "hpf", Hpf),
        new XAttribute(XNamespace.Xmlns + "dc", Dc),
        new XAttribute(XNamespace.Xmlns + "opf", Opf),
        new XAttribute(XNamespace.Xmlns + "hwpunitchar", UnitChar),
    };
    }
}
