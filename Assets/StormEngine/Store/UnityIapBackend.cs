using System;
using System.Collections.Generic;
using StormEngine.Core;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;

namespace StormEngine.Store
{
    /// <summary>
    /// Unity IAP 5 backend for <see cref="StoreService"/>. This assembly only compiles when
    /// com.unity.purchasing is installed, and registers itself before the first scene loads.
    /// Rewards are granted before an order is confirmed, so a crash mid-purchase is recovered
    /// on the next launch.
    /// </summary>
    public sealed class UnityIapBackend : StoreService.IBackend
    {
        /// <summary>
        /// The Editor uses the game's own test-purchase dialog by default: the IAP fake store's
        /// dialog creates a legacy-input EventSystem, which logs errors in an Input System only project.
        /// Set this to true to exercise the IAP fake store in the Editor anyway.
        /// </summary>
        const bool UseFakeStoreInEditor = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (Application.isEditor && !UseFakeStoreInEditor) return;
            StoreService.Factory = () => new UnityIapBackend();
        }

        StoreController _store;
        Action<bool, string> _inFlight;
        string _inFlightId;
        bool _ready;

        public string Name => "Unity IAP";
        public bool IsReady => _ready;
        public event Action<string> Recovered;
        public event Action<string> Restored;

        public async void Initialize(IList<ProductDef> products, Action<bool> done)
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                _store = UnityIAPServices.StoreController();
                _store.OnProductsFetched += _ =>
                {
                    _ready = true;
                    _store.FetchPurchases();
                    done?.Invoke(true);
                    done = null;
                };
                _store.OnProductsFetchFailed += failed =>
                {
                    Debug.LogWarning("[StormEngine] Store products could not be fetched: " + failed.FailureReason);
                    done?.Invoke(false);
                    done = null;
                };
                _store.OnPurchasePending += OnPending;
                _store.OnPurchaseConfirmed += _ => { };
                _store.OnPurchaseFailed += OnFailed;
                _store.OnPurchasesFetched += OnPurchasesFetched;
                _store.OnStoreDisconnected += reason =>
                {
                    _ready = false;
                    Debug.LogWarning("[StormEngine] Store disconnected: " + reason?.message);
                };
                _store.ProcessPendingOrdersOnPurchasesFetched(true);
                await _store.Connect();

                var definitions = new List<ProductDefinition>();
                foreach (var p in products)
                    definitions.Add(new ProductDefinition(p.Id, p.OneTime ? ProductType.NonConsumable : ProductType.Consumable));
                _store.FetchProducts(definitions);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Unity IAP could not start: " + e.Message);
                done?.Invoke(false);
            }
        }

        public string LocalizedPrice(string productId)
        {
            // The Editor's fake store reports placeholder prices; show the catalogue price instead.
            if (Application.isEditor || _store == null) return null;
            var product = _store.GetProductById(productId);
            return product?.metadata?.localizedPriceString;
        }

        public void Purchase(string productId, Action<bool, string> done)
        {
            if (!_ready || _store == null)
            {
                done(false, "The store is not available right now.");
                return;
            }
            if (_inFlight != null)
            {
                done(false, null);
                return;
            }
            _inFlight = done;
            _inFlightId = productId;
            try
            {
                _store.PurchaseProduct(productId);
            }
            catch (Exception e)
            {
                Finish(false, "Purchase failed: " + e.Message);
            }
        }

        void OnPending(PendingOrder order)
        {
            // Grant first, then confirm: if the app dies in between, the order is delivered again.
            foreach (var item in order.CartOrdered.Items())
            {
                string id = item.Product?.definition?.id;
                if (id == null) continue;
                if (_inFlight != null && id == _inFlightId) Finish(true, null);
                else Recovered?.Invoke(id);
            }
            _store.ConfirmPurchase(order);
        }

        void OnFailed(FailedOrder order)
        {
            if (_inFlight == null) return;
            bool cancelled = order.FailureReason == PurchaseFailureReason.UserCancelled;
            Finish(false, cancelled ? null : "Purchase failed: " + order.FailureReason);
        }

        void OnPurchasesFetched(Orders orders)
        {
            foreach (var confirmed in orders.ConfirmedOrders)
                foreach (var item in confirmed.CartOrdered.Items())
                {
                    var product = item.Product;
                    if (product?.definition != null && product.definition.type == ProductType.NonConsumable)
                        Restored?.Invoke(product.definition.id);
                }
        }

        void Finish(bool ok, string message)
        {
            var done = _inFlight;
            _inFlight = null;
            _inFlightId = null;
            done?.Invoke(ok, message);
        }
    }
}
