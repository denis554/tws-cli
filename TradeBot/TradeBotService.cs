using IBApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TradeBot.Events;
using TradeBot.FileIO;
using TradeBot.Generated;
using TradeBot.TwsAbstractions;

namespace TradeBot
{
    public class TradeBotService : TwsResponseHandler, INotifyPropertyValueChanged
    {
        private TwsClient client;
        private TickData tickData;
        private Contract contract;
        private TaskCompletionSource<IDictionary<string, PositionInfo>> positionRequestTCS;
        private IDictionary<string, PositionInfo> positions;

        private int currentTickerId;
        private int nextValidOrderId;

        public TradeBotService()
        {
            client = new TwsClient(this);
            tickData = new TickData();
        }

        #region Events
        public event PropertyValueChangedEventHandler PropertyValueChanged;
        public event Action ConnectionClosed;
        public event Action<TickData> TickDataUpdated;
        public event Action<int, int, string> ErrorOccured;
        #endregion

        #region Properties
        private string[] _managedAccounts;
        public string[] ManagedAccounts
        {
            get
            {
                return _managedAccounts;
            }
            private set
            {
                string[] oldValue = _managedAccounts;
                string[] newValue = value;
                _managedAccounts = newValue;
                RaisePropertyValueChangedEvent(oldValue, newValue);
            }
        }

        private string _tickerSymbol;
        public string TickerSymbol
        {
            get
            {
                return _tickerSymbol;
            }
            set
            {
                string oldValue = _tickerSymbol;
                string newValue = value?.Trim().ToUpper();

                // Don't do anything if the same ticker symbol is already selected.
                if (Equals(oldValue, newValue))
                {
                    return;
                }

                _tickerSymbol = newValue;

                if (!string.IsNullOrWhiteSpace(oldValue))
                {

                    client.cancelMktData(currentTickerId);
                    tickData = new TickData();
                    contract = null;
                }

                if (!string.IsNullOrWhiteSpace(newValue))
                {
                    contract = ContractFactory.CreateStockContract(newValue);
                    client.reqMktData(++currentTickerId, contract, "", false, null);
                }

                RaisePropertyValueChangedEvent(oldValue, newValue);
            }
        }

        private int _stepSize = -1;
        public int StepSize
        {
            get
            {
                return _stepSize;
            }
            set
            {
                int oldValue = _stepSize;
                int newValue = Math.Abs(value);
                _stepSize = newValue;
                RaisePropertyValueChangedEvent(oldValue, newValue);
            }
        }

        protected void RaisePropertyValueChangedEvent(object oldValue, object newValue, [CallerMemberName] string propertyName = null)
        {
            if (Equals(oldValue, newValue))
            {
                return;
            }

            PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(propertyName, oldValue, newValue));
        }
        #endregion

        #region Public methods
        public void Connect()
        {
            client.eConnect();
        }

        public void Disconnect()
        {
            client.eDisconnect();
        }

        public void LoadState()
        {
            AppState state = PropertySerializer.Deserialize<AppState>(PropertyFiles.STATE_FILE);

            TickerSymbol = state.TickerSymbol;
            StepSize = state.StepSize ?? -1;
        }

        public void SaveState()
        {
            AppState state = new AppState();
            state.TickerSymbol = TickerSymbol;
            state.StepSize = StepSize;

            PropertySerializer.Serialize(state, PropertyFiles.STATE_FILE);
        }

        public double GetTickData(int tickType)
        {
            return tickData[tickType];
        }

        public async Task<IDictionary<string, PositionInfo>> GetPositions()
        {
            positionRequestTCS = new TaskCompletionSource<IDictionary<string, PositionInfo>>();
            positions = new Dictionary<string, PositionInfo>();
            client.reqPositions();
            return await positionRequestTCS.Task;
        }

        public void PlaceOrder(OrderActions action, int totalQuantity, double price)
        {
            int orderId = nextValidOrderId++;
            Order order = OrderFactory.CreateLimitOrder(action, totalQuantity, price);
            client.placeOrder(orderId, contract, order);
        }
        #endregion

        #region TWS callbacks
        public override void managedAccounts(string accounts)
        {
            ManagedAccounts = accounts
                .Split(new string[] { "," }, StringSplitOptions.None)
                .Select(s => s.Trim())
                .ToArray();
        }

        public override void nextValidId(int nextValidOrderId)
        {
            this.nextValidOrderId = nextValidOrderId;
        }

        public override void tickPrice(int tickerId, int field, double price, int canAutoExecute)
        {
            UpdateTickData(tickerId, field, price);
        }

        public override void tickSize(int tickerId, int field, int size)
        {
            UpdateTickData(tickerId, field, size);
        }

        public override void tickGeneric(int tickerId, int field, double value)
        {
            UpdateTickData(tickerId, field, value);
        }

        public override void position(string account, Contract contract, int positionSize, double averageCost)
        {
            PositionInfo info = new PositionInfo(account, contract, positionSize, averageCost);
            positions.Add(contract.Symbol, info);
        }

        public override void positionEnd()
        {
            positionRequestTCS.SetResult(positions);
            positionRequestTCS = null;
            positions = null;
        }

        public override void connectionClosed()
        {
            ConnectionClosed?.Invoke();
        }

        public override void error(Exception exception)
        {
            error(exception.Message);
        }

        public override void error(string errorMessage)
        {
            error(-1, -1, errorMessage);
        }

        public override void error(int id, int errorCode, string errorMessage)
        {
            ErrorOccured?.Invoke(id, errorCode, errorMessage);

            switch (errorCode)
            {
                case ErrorCodes.TICKER_NOT_FOUND:
                    TickerSymbol = null;
                    break;
            }
        }

        public override void tickString(int tickerId, int field, string value)
        {
            // no-op, this is not needed for our basic feature set
            //base.tickString(tickerId, field, value);
        }
        #endregion

        #region Private methods
        private void UpdateTickData(int tickerId, int field, double value)
        {
            if (tickerId != currentTickerId)
            {
                return;
            }

            tickData[field] = value;

            TickDataUpdated?.Invoke(tickData);
        }
        #endregion
    }
}
