namespace ATAS.Indicators.Technical;

public partial class DomV3Indicator
{
	#region Nested types

	public struct DeletedOrderFlag
	{
		public decimal PriceRow { get; init; }

		public long OrderId { get; init; }
	}

	#endregion
}