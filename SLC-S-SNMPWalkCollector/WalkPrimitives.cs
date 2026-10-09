namespace SLCSSNMPWalkCollector
{
using System;
using System.Globalization;
using System.Text;

internal static class WalkPrimitives
{
public static bool IsValidOid(string oid)
{
if (String.IsNullOrWhiteSpace(oid))
{
return false;
}

foreach (string arc in oid.Split('.'))
{
ulong parsedArc;
if (!UInt64.TryParse(arc, NumberStyles.None, CultureInfo.InvariantCulture, out parsedArc))
{
return false;
}
}

return true;
}

public static bool TryCompareOids(string left, string right, out int comparison)
{
comparison = 0;
if (!IsValidOid(left) || !IsValidOid(right))
{
return false;
}

string[] leftArcs = left.Split('.');
string[] rightArcs = right.Split('.');
int arcCount = Math.Min(leftArcs.Length, rightArcs.Length);

for (int index = 0; index < arcCount; index++)
{
ulong leftArc = UInt64.Parse(leftArcs[index], CultureInfo.InvariantCulture);
ulong rightArc = UInt64.Parse(rightArcs[index], CultureInfo.InvariantCulture);
comparison = leftArc.CompareTo(rightArc);
if (comparison != 0)
{
return true;
}
}

comparison = leftArcs.Length.CompareTo(rightArcs.Length);
return true;
}

public static string EscapeJson(string value)
{
if (value == null)
{
return String.Empty;
}

StringBuilder builder = new StringBuilder(value.Length + 8);
foreach (char character in value)
{
switch (character)
{
case '\\': builder.Append("\\\\"); break;
case '"': builder.Append("\\\""); break;
case '\b': builder.Append("\\b"); break;
case '\f': builder.Append("\\f"); break;
case '\n': builder.Append("\\n"); break;
case '\r': builder.Append("\\r"); break;
case '\t': builder.Append("\\t"); break;
default:
if (character < ' ')
{
builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
}
else
{
builder.Append(character);
}

break;
}
}

return builder.ToString();
}
}
}