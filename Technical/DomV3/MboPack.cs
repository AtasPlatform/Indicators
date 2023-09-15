namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;

using ATAS.DataFeedsCore;
using ATAS.Indicators;

public partial class DomV3
{
	#region Nested types

	public class MboPack
	{
		#region Fields

		#endregion

		#region Properties

		public IEnumerable<MarketByOrder> Orders { set; get; }

		public IEnumerable<MarketDataArg> Trades { set; get; }

		public ulong KeyIndex { set; get; }

		#endregion

		#region ctor

		public MboPack()
		{
			Orders = Array.Empty<MarketByOrder>();
			Trades = Array.Empty<MarketDataArg>();
			KeyIndex = 0;
		}

		public MboPack(IEnumerable<MarketByOrder> orders, IEnumerable<MarketDataArg> trades, ulong keyIndex)
		{
			this.Orders = orders;
			this.Trades = trades;
			this.KeyIndex = keyIndex;
		}

		public MboPack(IEnumerable<MarketByOrder> orders)
		{
			this.Orders = orders;
			Trades = Array.Empty<MarketDataArg>();
			KeyIndex = (ulong)DateTime.Now.Ticks;
		}

		public MboPack(IEnumerable<MarketDataArg> trades)
		{
			this.Trades = trades;
			Orders = Array.Empty<MarketByOrder>();
			KeyIndex = (ulong)DateTime.Now.Ticks;
		}

		#endregion
	}

	#endregion
}