using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    public static class FormulaFunctions
    {
        private const string FuturePrefix = "_xlfn.";
        private const string WorksheetPrefix = "_xlfn._xlws.";

        private static readonly Dictionary<string, (int Min, int Max)> s_arity = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SUM"] = (1, 255),
            ["AVERAGE"] = (1, 255),
            ["MIN"] = (1, 255),
            ["MAX"] = (1, 255),
            ["COUNT"] = (1, 255),
            ["COUNTA"] = (1, 255),
            ["ROUND"] = (2, 2),
            ["ROUNDUP"] = (2, 2),
            ["ROUNDDOWN"] = (2, 2),
            ["IF"] = (2, 3),
            ["IFERROR"] = (2, 2),
            ["SUMIF"] = (2, 3),
            ["COUNTIF"] = (2, 2),
            ["AVERAGEIF"] = (2, 3),
            ["SUMIFS"] = (3, 255),
            ["COUNTIFS"] = (2, 254),
            ["AVERAGEIFS"] = (3, 255),
            ["MAXIFS"] = (3, 255),
            ["MINIFS"] = (3, 255),
            ["AND"] = (1, 255),
            ["OR"] = (1, 255),
            ["NOT"] = (1, 1),
            ["ABS"] = (1, 1),
            ["INT"] = (1, 1),
            ["MOD"] = (2, 2),
            ["TRUNC"] = (1, 2),
            ["POWER"] = (2, 2),
            ["SQRT"] = (1, 1),
            ["PRODUCT"] = (1, 255),
            ["SUMPRODUCT"] = (1, 255),
            ["SUBTOTAL"] = (2, 255),
            ["MEDIAN"] = (1, 255),
            ["RANK"] = (2, 3),
            ["RANK.EQ"] = (2, 3),
            ["LARGE"] = (2, 2),
            ["SMALL"] = (2, 2),
            ["VLOOKUP"] = (3, 4),
            ["HLOOKUP"] = (3, 4),
            ["INDEX"] = (2, 4),
            ["MATCH"] = (2, 3),
            ["XLOOKUP"] = (3, 6),
            ["LEFT"] = (1, 2),
            ["RIGHT"] = (1, 2),
            ["MID"] = (3, 3),
            ["LEN"] = (1, 1),
            ["TRIM"] = (1, 1),
            ["FIND"] = (2, 3),
            ["SEARCH"] = (2, 3),
            ["SUBSTITUTE"] = (3, 4),
            ["CONCAT"] = (1, 255),
            ["CONCATENATE"] = (1, 255),
            ["TEXTJOIN"] = (3, 255),
            ["TEXT"] = (2, 2),
            ["UPPER"] = (1, 1),
            ["LOWER"] = (1, 1),
            ["PROPER"] = (1, 1),
            ["REPT"] = (2, 2),
            ["VALUE"] = (1, 1),
            ["EXACT"] = (2, 2),
            ["TODAY"] = (0, 0),
            ["NOW"] = (0, 0),
            ["DATE"] = (3, 3),
            ["YEAR"] = (1, 1),
            ["MONTH"] = (1, 1),
            ["DAY"] = (1, 1),
            ["WEEKDAY"] = (1, 2),
            ["EDATE"] = (2, 2),
            ["EOMONTH"] = (2, 2),
            ["DATEDIF"] = (3, 3),
            ["NETWORKDAYS"] = (2, 3),
            ["WORKDAY"] = (2, 3),
            ["ISNUMBER"] = (1, 1),
            ["ISTEXT"] = (1, 1),
            ["ISBLANK"] = (1, 1),
            ["ISERROR"] = (1, 1),
            ["ISERR"] = (1, 1),
            ["ISNA"] = (1, 1),
            ["IFNA"] = (2, 2),
            ["NA"] = (0, 0),
        };

        private static readonly HashSet<string> s_future = new(Split(
            "ACOT ACOTH AGGREGATE ANCHORARRAY ARABIC ARRAYTOTEXT BASE BETA.DIST BETA.INV BINOM.DIST BINOM.DIST.RANGE BINOM.INV BITAND BITLSHIFT BITOR BITRSHIFT BITXOR "
            + "BYCOL BYROW CEILING.MATH CEILING.PRECISE CHISQ.DIST CHISQ.DIST.RT CHISQ.INV CHISQ.INV.RT CHISQ.TEST CHOOSECOLS CHOOSEROWS COMBINA CONCAT CONFIDENCE.NORM "
            + "CONFIDENCE.T COT COTH COVARIANCE.P COVARIANCE.S CSC CSCH DAYS DECIMAL DETECTLANGUAGE DROP ERF.PRECISE ERFC.PRECISE EXPAND EXPON.DIST F.DIST F.DIST.RT F.INV "
            + "F.INV.RT F.TEST FIELDVALUE FILTERXML FLOOR.MATH FLOOR.PRECISE FORECAST.ETS FORECAST.ETS.CONFINT FORECAST.ETS.SEASONALITY FORECAST.ETS.STAT FORECAST.LINEAR "
            + "FORMULATEXT GAMMA GAMMA.DIST GAMMA.INV GAMMALN.PRECISE GAUSS GROUPBY HSTACK HYPGEOM.DIST IFNA IFS IMAGE IMCOSH IMCOT IMCSC IMCSCH IMSEC IMSECH IMSINH IMTAN "
            + "ISFORMULA ISOMITTED ISOWEEKNUM LAMBDA LET LOGNORM.DIST LOGNORM.INV MAKEARRAY MAP MAXIFS MINIFS MODE.MULT MODE.SNGL MUNIT NEGBINOM.DIST NETWORKDAYS.INTL "
            + "NORM.DIST NORM.INV NORM.S.DIST NORM.S.INV NUMBERVALUE PDURATION PERCENTILE.EXC PERCENTILE.INC PERCENTOF PERCENTRANK.EXC PERCENTRANK.INC PERMUTATIONA PHI "
            + "PIVOTBY POISSON.DIST QUARTILE.EXC QUARTILE.INC RANDARRAY RANK.AVG RANK.EQ REDUCE REGEXEXTRACT REGEXREPLACE REGEXTEST RRI SCAN SEC SECH SEQUENCE SHEET SHEETS "
            + "SINGLE SKEW.P SORTBY STDEV.P STDEV.S STOCKHISTORY SWITCH T.DIST T.DIST.2T T.DIST.RT T.INV T.INV.2T T.TEST TAKE TEXTAFTER TEXTBEFORE TEXTJOIN TEXTSPLIT "
            + "TOCOL TOROW TRANSLATE TRIMRANGE UNICHAR UNICODE UNIQUE VALUETOTEXT VAR.P VAR.S VSTACK WEBSERVICE WEIBULL.DIST WORKDAY.INTL WRAPCOLS WRAPROWS XLOOKUP "
            + "XMATCH XOR Z.TEST"), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> s_futureWorksheet = new(Split("FILTER SORT"), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> s_excel = new(Split(
            "ABS ACCRINT ACCRINTM ACOS ACOSH ADDRESS AMORDEGRC AMORLINC AND AREAS ASC ASIN ASINH ATAN ATAN2 ATANH AVEDEV AVERAGE AVERAGEA AVERAGEIF AVERAGEIFS "
            + "BAHTTEXT BESSELI BESSELJ BESSELK BESSELY BETADIST BETAINV BIN2DEC BIN2HEX BIN2OCT BINOMDIST CALL CEILING CELL CHAR CHIDIST CHIINV CHITEST CHOOSE CLEAN CODE "
            + "COLUMN COLUMNS COMBIN COMPLEX CONCATENATE CONFIDENCE CONVERT CORREL COS COSH COUNT COUNTA COUNTBLANK COUNTIF COUNTIFS COUPDAYBS COUPDAYS COUPDAYSNC COUPNCD "
            + "COUPNUM COUPPCD COVAR CRITBINOM CUBEKPIMEMBER CUBEMEMBER CUBEMEMBERPROPERTY CUBERANKEDMEMBER CUBESET CUBESETCOUNT CUBEVALUE CUMIPMT CUMPRINC DATE DATEDIF "
            + "DATEVALUE DAVERAGE DAY DAYS360 DB DBCS DCOUNT DCOUNTA DDB DEC2BIN DEC2HEX DEC2OCT DEGREES DELTA DEVSQ DGET DISC DMAX DMIN DOLLAR DOLLARDE DOLLARFR DPRODUCT "
            + "DSTDEV DSTDEVP DSUM DURATION DVAR DVARP EDATE EFFECT ENCODEURL EOMONTH ERF ERFC ERROR.TYPE EUROCONVERT EVEN EXACT EXP EXPONDIST FACT FACTDOUBLE FALSE FDIST "
            + "FIND FINDB FINV FISHER FISHERINV FIXED FLOOR FORECAST FREQUENCY FTEST FV FVSCHEDULE GAMMADIST GAMMAINV GAMMALN GCD GEOMEAN GESTEP GETPIVOTDATA GROWTH HARMEAN "
            + "HEX2BIN HEX2DEC HEX2OCT HLOOKUP HOUR HYPERLINK HYPGEOMDIST IF IFERROR IMABS IMAGINARY IMARGUMENT IMCONJUGATE IMCOS IMDIV IMEXP IMLN IMLOG10 IMLOG2 IMPOWER "
            + "IMPRODUCT IMREAL IMSIN IMSQRT IMSUB IMSUM INDEX INDIRECT INFO INT INTERCEPT INTRATE IPMT IRR ISBLANK ISERR ISERROR ISEVEN ISLOGICAL ISNA ISNONTEXT ISNUMBER "
            + "ISO.CEILING ISODD ISPMT ISREF ISTEXT JIS KURT LARGE LCM LEFT LEFTB LEN LENB LINEST LN LOG LOG10 LOGEST LOGINV LOGNORMDIST LOOKUP LOWER MATCH MAX MAXA MDETERM "
            + "MDURATION MEDIAN MID MIDB MIN MINA MINUTE MINVERSE MIRR MMULT MOD MODE MONTH MROUND MULTINOMIAL N NA NEGBINOMDIST NETWORKDAYS NOMINAL NORMDIST NORMINV "
            + "NORMSDIST NORMSINV NOT NOW NPER NPV OCT2BIN OCT2DEC OCT2HEX ODD ODDFPRICE ODDFYIELD ODDLPRICE ODDLYIELD OFFSET OR PEARSON PERCENTILE PERCENTRANK PERMUT "
            + "PHONETIC PI PMT POISSON POWER PPMT PRICE PRICEDISC PRICEMAT PROB PRODUCT PROPER PV QUARTILE QUOTIENT RADIANS RAND RANDBETWEEN RANK RATE RECEIVED REGISTER.ID "
            + "REPLACE REPLACEB REPT RIGHT RIGHTB ROMAN ROUND ROUNDDOWN ROUNDUP ROW ROWS RSQ RTD SEARCH SEARCHB SECOND SERIESSUM SIGN SIN SINH SKEW SLN SLOPE SMALL SQL.REQUEST "
            + "SQRT SQRTPI STANDARDIZE STDEV STDEVA STDEVP STDEVPA STEYX SUBSTITUTE SUBTOTAL SUM SUMIF SUMIFS SUMPRODUCT SUMSQ SUMX2MY2 SUMX2PY2 SUMXMY2 SYD T TAN TANH "
            + "TBILLEQ TBILLPRICE TBILLYIELD TDIST TEXT TIME TIMEVALUE TINV TODAY TRANSPOSE TREND TRIM TRIMMEAN TRUE TRUNC TTEST TYPE UPPER USDOLLAR VALUE VAR VARA VARP VARPA "
            + "VDB VLOOKUP WEEKDAY WEEKNUM WEIBULL WORKDAY XIRR XNPV YEAR YEARFRAC YIELD YIELDDISC YIELDMAT ZTEST"), StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> Supported => s_arity.Keys;

        public static string SupportedList => string.Join(", ", s_arity.Keys.OrderBy(n => n, StringComparer.Ordinal));

        public static string Normalize(string name)
        {
            var value = (name ?? string.Empty).ToUpperInvariant();
            foreach (var prefix in new[] { "_XLFN._XLWS.", "_XLFN.", "_XLWS." })
            {
                if (value.StartsWith(prefix, StringComparison.Ordinal))
                    return value.Substring(prefix.Length);
            }

            return value;
        }

        public static bool IsSupported(string name) => s_arity.ContainsKey(Normalize(name));

        public static bool AcceptsCount(string name, int count) =>
            !s_arity.TryGetValue(Normalize(name), out var arity) || (count >= arity.Min && count <= arity.Max);

        public static bool IsExcelFunction(string name)
        {
            var normalized = Normalize(name);
            return s_arity.ContainsKey(normalized) || s_excel.Contains(normalized) || s_future.Contains(normalized) || s_futureWorksheet.Contains(normalized);
        }

        public static string StorageName(string name)
        {
            var normalized = Normalize(name);
            if (s_futureWorksheet.Contains(normalized))
                return WorksheetPrefix + normalized;

            return s_future.Contains(normalized) ? FuturePrefix + normalized : normalized;
        }

        public static void Validate(FormulaNode root)
        {
            foreach (var node in root.Descendants().Where(n => n.Kind == FormulaNodeKind.Function))
            {
                var name = Normalize(node.Value);
                if (!s_arity.TryGetValue(name, out var arity))
                    continue;

                var count = node.Children.Count;
                if (count == 1 && arity.Min == 0 && node.Children[0].Kind == FormulaNodeKind.Missing)
                    count = 0;

                if (count < arity.Min || count > arity.Max)
                {
                    var expected = arity.Max == 0 ? "없어야" : arity.Min == arity.Max ? $"{arity.Min}개" : arity.Max >= 254 ? $"{arity.Min}개 이상" : $"{arity.Min}~{arity.Max}개";
                    throw new FormulaException(arity.Max == 0 ? $"{name} 함수에는 값이 들어가지 않아요. {name}()처럼 써 주세요." : $"{name} 함수에는 값이 {expected} 들어가야 해요.");
                }

                if (name is "SUMIFS" or "AVERAGEIFS" or "MAXIFS" or "MINIFS" && count % 2 == 0)
                    throw new FormulaException($"{name} 함수는 (계산할 범위, 조건 범위, 조건, …) 순서로 써야 해요.");

                if (name == "COUNTIFS" && count % 2 == 1)
                    throw new FormulaException("COUNTIFS 함수는 (조건 범위, 조건, …) 짝으로 써야 해요.");

                foreach (var index in RangeArguments(name, count))
                {
                    if (node.Children[index].Kind is not (FormulaNodeKind.Reference or FormulaNodeKind.Missing or FormulaNodeKind.Name))
                        throw new FormulaException($"{name} 함수의 {index + 1}번째 값은 B2:B40 같은 범위여야 해요.");
                }
            }
        }

        private static IEnumerable<int> RangeArguments(string name, int count)
        {
            switch (name)
            {
                case "SUMIF":
                case "AVERAGEIF":
                    yield return 0;
                    if (count > 2)
                        yield return 2;

                    break;
                case "COUNTIF":
                    yield return 0;
                    break;
                case "SUBTOTAL":
                    for (var i = 1; i < count; i++)
                        yield return i;

                    break;
                case "SUMIFS":
                case "AVERAGEIFS":
                case "MAXIFS":
                case "MINIFS":
                    yield return 0;
                    for (var i = 1; i < count; i += 2)
                        yield return i;

                    break;
                case "COUNTIFS":
                    for (var i = 0; i < count; i += 2)
                        yield return i;

                    break;
            }
        }

        private static IEnumerable<string> Split(string names) => names.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
