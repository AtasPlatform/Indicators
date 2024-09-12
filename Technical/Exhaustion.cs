namespace ATAS.Indicators.Technical;

using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Utils.Common.Collections;

using FilterColor2 = ATAS.Indicators.FilterColor;

[FeatureId("NotReady")]
[Category(IndicatorCategories.ClustersProfilesLevels)]
[DisplayName("Exhaustion")]
public class Exhaustion : Indicator
{
    #region Nested types

    public enum CalcModes
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bid))]
        Bid,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ask))]
        Ask,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BidAndAsk))]
        BidAndAsk,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume))]
        Volume
    }

    #endregion

    #region Fields
    private const string _indicatorName = "Exhaustion";
    private readonly PriceSelectionDataSeries _topSelection = new("TopSelection ", "Top");
    private readonly PriceSelectionDataSeries _bottomSelection = new("BottomSelection", "Bottom");

    private int _lastBar = -1;
    private decimal _step;
    private bool _toAlert;
    private string _alertMessage = string.Empty;
    private bool _alerted;

    private CalcModes _calcMode;
    private int _amoutOfPrices = 5;
    private bool _useAlerts;
    private FilterString _alertFile;
    private FilterBool _onBarCloseAlert;

    private ObjectType _visualType = ObjectType.Rectangle;
    private CrossColor _topColor = DefaultColors.Maroon.Convert().GetWithTransparency(70);
    private FilterColor2 _topClusterColor;
    private CrossColor _bottomColor = DefaultColors.Lime.Convert().GetWithTransparency(70);
    private FilterColor2 _bottomClusterColor;
    private bool _showPriceSelection = true;
    private int _size = 10; 

    #endregion

    #region Properties

    #region Settings

    [Parameter]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.CalculationMode), Description = nameof(Strings.CalculationModeDescription))]
    public CalcModes CalcMode
    {
        get => _calcMode;
        set
        {
            _calcMode = value;
            RecalculateValues();
        }
    }

    [Parameter]
    [Range(2, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.DepthLevelsCount), Description = nameof(Strings.PriceLevelsCountDescription))]
    public int AmoutOfPrices 
    { 
        get => _amoutOfPrices;
        set
        {
            _amoutOfPrices = value;
            RecalculateValues();
        }
    }

    #endregion

    #region Visualization

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.VisualMode), Description = nameof(Strings.VisualModeDescription))]
    public ObjectType VisualType
    {
        get => _visualType;
        set
        {
            _visualType = value;
            
            ForAllPriceSelectionValuesAction(_topSelection, (x) => x.VisualObject = value);
            ForAllPriceSelectionValuesAction(_bottomSelection, (x) => x.VisualObject = value);
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.ShowPriceSelection), Description = nameof(Strings.ShowPriceSelectionDescription))]
    public bool ShowPriceSelection
    {
        get => _showPriceSelection;
        set
        {
            _showPriceSelection = value;
            TopClusterColor.SetEnabled(value);
            BottomClusterColor.SetEnabled(value);

            SetDataSeriesPriceSelectionColor(_topColor, _topSelection, value);
            SetDataSeriesPriceSelectionColor(_bottomColor, _bottomSelection, value);
        }
    }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.Size), Description = nameof(Strings.SizeDescription))]
    public int Size
    {
        get => _size;
        set
        {
            _size = value;

            ForAllPriceSelectionValuesAction(_topSelection, (x) => x.Size = value);
            ForAllPriceSelectionValuesAction(_bottomSelection, (x) => x.Size = value);
        }
    }

    #endregion

    #region ResistanceLevel

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.ResistanceLevel), Name = nameof(Strings.ObjectsColor), Description = nameof(Strings.VisualObjectsDescription))]
    public CrossColor TopColor
    {
        get => _topColor;
        set
        {
            _topColor = value;

            SetDataSeriesObjectColor(value, _topSelection);
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.ResistanceLevel), Name = nameof(Strings.PriceSelectionColor), Description = nameof(Strings.PriceSelectionColorDescription))]
    public FilterColor2 TopClusterColor
    {
        get => _topClusterColor;
        set => SetTrackedProperty(ref _topClusterColor, value, (_) =>
        {
            SetDataSeriesPriceSelectionColor(value.Value, _topSelection);
            RedrawChart();
        });
    }

    #endregion

    #region SupportLevel

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.SupportLevel), Name = nameof(Strings.ObjectsColor), Description = nameof(Strings.VisualObjectsDescription))]
    public CrossColor BottomColor
    {
        get => _bottomColor;
        set
        {
            _bottomColor = value;

            SetDataSeriesObjectColor(value, _bottomSelection);
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.SupportLevel), Name = nameof(Strings.PriceSelectionColor), Description = nameof(Strings.PriceSelectionColorDescription))]
    public FilterColor2 BottomClusterColor
    {
        get => _bottomClusterColor;
        set => SetTrackedProperty(ref _bottomClusterColor, value, (_) =>
        {
            SetDataSeriesPriceSelectionColor(value.Value, _bottomSelection);
            RedrawChart();
        });
    }

    #endregion

    #region Alerts

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Alerts), Name = nameof(Strings.UseAlert), Description = nameof(Strings.UseAlertDescription))]
    public bool UseAlerts 
    { 
        get => _useAlerts; 
        set
        {
            _useAlerts = value;
            AlertFile.SetEnabled(value);
            OnBarCloseAlert.SetEnabled(value);
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Alerts), Name = nameof(Strings.AlertFile), Description = nameof(Strings.AlertFileDescription))]
    public FilterString AlertFile 
    { 
        get => _alertFile;
        set => SetProperty(ref _alertFile, value);
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Alerts), Name = nameof(Strings.OnBarClose), Description = nameof(Strings.OnBarCloseDescription))]
    public FilterBool OnBarCloseAlert
    { 
        get => _onBarCloseAlert;
        set => SetProperty(ref _onBarCloseAlert, value);
    }

    #endregion

    #endregion

    #region ctor

    public Exhaustion() : base(true)
    {
        DenyToChangePanel = true;
        _topSelection.IsHidden = true;
        _bottomSelection.IsHidden = true;

        DataSeries[0] = _topSelection;
        DataSeries.Add(_bottomSelection);

        AlertFile = new (false) { Value = "alert1" };
        OnBarCloseAlert = new (false) { Value = true };
        TopClusterColor = new(false) { Value = DefaultColors.Red.Convert().GetWithTransparency(20) };
        BottomClusterColor = new(false) { Value = DefaultColors.Green.Convert().GetWithTransparency(20) };
    }

    #endregion

    #region Protected methods

    protected override void OnInitialize()
    {
        AlertFile.SetEnabled(UseAlerts);
        OnBarCloseAlert.SetEnabled(UseAlerts);
        TopClusterColor.SetEnabled(ShowPriceSelection);
        BottomClusterColor.SetEnabled(ShowPriceSelection);
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar != _lastBar) 
        {
            if (bar == 0)
                _step = ChartInfo.PriceChartContainer.Step;
            else if (bar == CurrentBar - 1 && UseAlerts)
            {
                if (_toAlert)
                    AddAlert(AlertFile.Value, _alertMessage);

                _alerted = false;
            }
        }

        if (bar == CurrentBar - 1)
        {
            _topSelection[bar].Clear();
            _bottomSelection[bar].Clear();
            _toAlert = false;
            _alertMessage = string.Empty;
        }

        switch (CalcMode)
        {
            case CalcModes.Bid:
                CalcFromLow(bar, CalcModes.Bid, "Bids", SelectionType.Bid);               

                break;
            case CalcModes.Ask:
                CalcFromHigh(bar, CalcModes.Ask, "Asks", SelectionType.Ask);

                break;
            case CalcModes.BidAndAsk:
                CalcFromLow(bar, CalcModes.Bid, "Bids", SelectionType.Bid);
                CalcFromHigh(bar, CalcModes.Ask, "Asks", SelectionType.Ask);

                break;
            case CalcModes.Volume:
                CalcFromLow(bar, CalcModes.Volume, "Lots", SelectionType.Full);
                CalcFromHigh(bar, CalcModes.Volume, "Lots", SelectionType.Full);

                break;
        }

        _lastBar = bar;
    }

    #endregion

    #region Private methods

    private void CalcFromHigh
    (
        int bar,
        CalcModes sourceType,
        string sourceMode,
        SelectionType selectionType
    )
    {
        var candle = GetCandle(bar);
        var pvInfos = new List<(decimal, decimal)>();
        var prevSourceValue = 0m;
        var count = 0;

        for (decimal i = candle.High; i >= candle.Low; i -= _step)
        {
            var info = candle.GetPriceVolumeInfo(i);
            count++;

            if (count > _amoutOfPrices)
                break;

            if (info is null)
                return;

            var sourceValue = GetSourceValue(sourceType, info);

            if (i == candle.High)
            {
                prevSourceValue = sourceValue;
                pvInfos.Add((info.Price, sourceValue));
                continue;
            }

            if (sourceValue > prevSourceValue)
            {
                pvInfos.Add((info.Price, sourceValue));
                prevSourceValue = sourceValue;
            }
            else
                break;
        }

        if (pvInfos.Count != _amoutOfPrices)
            return;

        var newPriceSelection = GetNewPriceSelection(pvInfos, selectionType, _topColor, sourceMode);
        _topSelection[bar].AddRange(newPriceSelection);

        AddClusterAlert(bar, newPriceSelection);
    }

    private void CalcFromLow
    (
        int bar, 
        CalcModes sourceType,
        string sourceMode,
        SelectionType selectionType
    )
    {
        var candle = GetCandle(bar);
        var pvInfos = new List<(decimal, decimal)>();
        var prevSourceValue = 0m;
        var count = 0;

        for (decimal i = candle.Low; i <= candle.High; i += _step)
        {
            var info = candle.GetPriceVolumeInfo(i);
            count++;

            if (count > _amoutOfPrices)
                break;

            if (info is null)
                return;

            var sourceValue = GetSourceValue(sourceType, info);

            if (i == candle.Low)
            {
                prevSourceValue = sourceValue;
                pvInfos.Add((info.Price, sourceValue));
                continue;
            }

            if (sourceValue > prevSourceValue)
            {
                pvInfos.Add((info.Price, sourceValue));
                prevSourceValue = sourceValue;
            }
            else
                break;
        }

        if (pvInfos.Count != _amoutOfPrices)
            return;

        pvInfos.Reverse();
        var newPriceSelection = GetNewPriceSelection(pvInfos, selectionType, _bottomColor, sourceMode);
        _bottomSelection[bar].AddRange(newPriceSelection);

        AddClusterAlert(bar, newPriceSelection);      
    }

    private void AddClusterAlert(int bar, PriceSelectionValue[] newPriceSelection)
    {
        if (!UseAlerts || bar != CurrentBar - 1 || _alerted)
            return;

        var alertMessage = string.Join(Environment.NewLine, newPriceSelection.Select(ps => ps.Tooltip));

        if (!OnBarCloseAlert.Value)
        {
            AddAlert(AlertFile.Value, alertMessage);
            _alerted = true;
        }
        else
        {
            _toAlert = true;
            _alertMessage += $"{alertMessage}{Environment.NewLine}";
        }
    }

    private decimal GetSourceValue(CalcModes sourceType, PriceVolumeInfo info)
    {
        return sourceType switch
        {
            CalcModes.Bid => info.Bid,
            CalcModes.Ask => info.Ask,
            _ => info.Volume,
        };
    }

    private PriceSelectionValue[] GetNewPriceSelection(List<(decimal, decimal)> pvInfos, SelectionType selectionType, CrossColor color, string sourceMode)
    {
        var result = new PriceSelectionValue[pvInfos.Count];

        for (int i = 0; i < pvInfos.Count; i++)
        {
            var indName = i == 0 ? $"{_indicatorName}{Environment.NewLine}" : string.Empty;
            var volume = pvInfos[i].Item2;
            var tooltip = $"{ChartInfo.TryGetMinimizedVolumeString(volume)} {sourceMode}";

            result[i] = new PriceSelectionValue(pvInfos[i].Item1)
            {
                VisualObject = VisualType,
                Size = Size,
                SelectionSide = selectionType,
                ObjectColor = color,
                PriceSelectionColor = ShowPriceSelection ? color : CrossColors.Transparent,
                Tooltip = $"{indName}{tooltip}",
                Context = volume,
                MinimumPrice = pvInfos.Select(e => e.Item1).Min(),
                MaximumPrice = pvInfos.Select(e => e.Item1).Max()
            };
        }

        return result;
    }

    private void ForAllPriceSelectionValuesAction(PriceSelectionDataSeries dataSeries, Action<PriceSelectionValue> action)
    {
        for (var i = 0; i < dataSeries.Count; i++)
        {
            dataSeries[i].ForEach(action);
        }
    }

    private void SetDataSeriesObjectColor(CrossColor color, PriceSelectionDataSeries dataSeries)
    {
        for (var i = 0; i < dataSeries.Count; i++)
            dataSeries[i].ForEach(x =>
            {
                x.ObjectColor = color;
            });
    }

    private void SetDataSeriesPriceSelectionColor(CrossColor color, PriceSelectionDataSeries dataSeries, bool showPriceSelection = true)
    {
        ForAllPriceSelectionValuesAction(dataSeries, (x) =>
        {
            x.PriceSelectionColor = showPriceSelection ? color : CrossColors.Transparent;
        });
    }

    #endregion
}
