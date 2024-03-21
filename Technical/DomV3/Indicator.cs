namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;

using ATAS.DataFeedsCore;
using ATAS.Indicators;

using OFT.Attributes;
using OFT.Rendering.Context;

[DisplayName("DOM V3")]
[FeatureId("NotApproved")]
public partial class DomV3 : Indicator
{
	#region Fields

	private MboController? _controller;

	#endregion

	#region ctor

	public DomV3()
	{
		IsVerticalIndicator = true;
		EnableCustomDrawing = true;
		Panel = IndicatorDataProvider.NewPanel;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
	}

	#endregion

	#region Protected methods

	protected override void OnDispose()
	{
		_controller?.Dispose();
	}

	protected override async void OnInitialize()
	{
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		
		await SubscribeMarketByOrderData();

		var mboCache = GetMarketByOrdersCache(TimeSpan.Zero);

		_controller = new MboController(mboCache);
	}

	protected override void OnMarketByOrdersChanged(IEnumerable<MarketByOrder> values)
	{
		_controller?.AddMarketByOrder(values);
	}

    protected override void OnNewTrades(IEnumerable<MarketDataArg> trades)
	{
		_controller?.AddTrades(trades);
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo != null && Container != null)
			_controller?.RenderSnapshot(context, Container, ChartInfo);
	}

	protected override void OnCalculate(int bar, decimal value)
	{
	}

	#endregion
}