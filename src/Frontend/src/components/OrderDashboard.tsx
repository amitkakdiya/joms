import { useCallback, useEffect, useState } from "react";
import {
  advanceProduction,
  assignOrderToBilling,
  assignOrderToCad,
  assignOrderToProduction,
  completeOrderBilling,
  decideOrderDesign,
  fetchBillingUsers,
  fetchCadUsers,
  fetchOrders,
  fetchProductionUsers,
  markOrderDispatched,
  submitOrder,
  submitOrderDesign,
  type CadUser,
  type OrderItem,
  type WorkflowUser,
} from "./orders";
import "./OrderDashboard.css";

type OrderDashboardProps = {
  accessToken: string;
  role: string;
};

const productionNextStage: Record<string, string> = {
  ProductionStarted: "BagGenerated",
  BagGenerated: "Production",
  Production: "Casting",
  Casting: "Rhodium",
  Rhodium: "FinishBag",
};

const stageLabels: Record<string, string> = {
  BagGenerated: "Bag Generate",
  Production: "Production",
  Casting: "Casting",
  Rhodium: "Rhodium",
  FinishBag: "Finish Bag",
};

const statusLabels: Record<string, string> = {
  Submitted: "Submitted",
  AssignedToCad: "Assigned to CAD",
  DesignSubmitted: "Awaiting customer approval",
  DesignApproved: "Customer approved",
  DesignRejected: "Revision requested",
  AssignedToProduction: "Assigned to production",
  ProductionStarted: "Production started",
  BagGenerated: "Bag generated",
  Production: "Production",
  Casting: "Casting",
  Rhodium: "Rhodium",
  FinishBag: "Finish bag complete",
  AssignedToBilling: "Assigned to final billing",
  ReadyForDispatch: "Ready for dispatch",
  Dispatched: "Dispatched",
};

export function OrderDashboard({ accessToken, role }: OrderDashboardProps) {
  const [orders, setOrders] = useState<OrderItem[] | null>(null);
  const [cadUsers, setCadUsers] = useState<CadUser[]>([]);
  const [productionUsers, setProductionUsers] = useState<WorkflowUser[]>([]);
  const [billingUsers, setBillingUsers] = useState<WorkflowUser[]>([]);
  const [description, setDescription] = useState("");
  const [selectedCadIds, setSelectedCadIds] = useState<Record<number, number>>({});
  const [selectedProductionIds, setSelectedProductionIds] = useState<Record<number, number>>({});
  const [selectedBillingIds, setSelectedBillingIds] = useState<Record<number, number>>({});
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isAdmin = role === "Admin";
  const isCustomer = role === "Customer";
  const isCad = role === "CAD";
  const isProduction = role === "Production";
  const isBilling = role === "Billing";
  const canUseOrders = isAdmin || isCustomer || isCad || isProduction || isBilling;

  const refresh = useCallback(async () => {
    const orderList = await fetchOrders(accessToken);
    setOrders(orderList);

    const lookupResults = await Promise.allSettled([
      isAdmin ? fetchCadUsers(accessToken) : Promise.resolve([] as CadUser[]),
      isAdmin ? fetchProductionUsers(accessToken) : Promise.resolve([] as WorkflowUser[]),
      isProduction ? fetchBillingUsers(accessToken) : Promise.resolve([] as WorkflowUser[]),
    ]);
    const [cadResult, productionResult, billingResult] = lookupResults;
    const lookupErrors: string[] = [];

    if (cadResult.status === "fulfilled") setCadUsers(cadResult.value);
    else {
      setCadUsers([]);
      lookupErrors.push("CAD users could not load.");
    }

    if (productionResult.status === "fulfilled") setProductionUsers(productionResult.value);
    else {
      setProductionUsers([]);
      lookupErrors.push("Production users could not load.");
    }

    if (billingResult.status === "fulfilled") setBillingUsers(billingResult.value);
    else {
      setBillingUsers([]);
      lookupErrors.push("Billing users could not load.");
    }

    setError(lookupErrors.length > 0 ? `Orders loaded, but ${lookupErrors.join(" ")}` : null);
  }, [accessToken, isAdmin, isProduction]);

  useEffect(() => {
    let alive = true;
    async function loadOrders() {
      try {
        await refresh();
      } catch (requestError) {
        if (alive) {
          setError(requestError instanceof Error ? requestError.message : "Orders could not load.");
        }
      }
    }
    void loadOrders();
    return () => {
      alive = false;
    };
  }, [refresh]);

  async function performOrderAction(action: () => Promise<unknown>) {
    setSaving(true);
    setError(null);
    try {
      await action();
      await refresh();
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "The order could not be updated.");
    } finally {
      setSaving(false);
    }
  }

  async function onSubmitOrder(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await submitOrder(accessToken, description.trim());
      setDescription("");
      await refresh();
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Order could not be submitted.");
    } finally {
      setSaving(false);
    }
  }

  if (!canUseOrders) {
    return (
      <section className="orders-card">
        <h2>Order dashboard</h2>
        <p className="orders-muted">This account does not have an order workflow role.</p>
      </section>
    );
  }

  const workspace = isAdmin
    ? "Admin workspace"
    : isCustomer
      ? "Customer workspace"
      : isCad
        ? "CAD workspace"
        : isProduction
          ? "Production workspace"
          : "Billing workspace";
  const heading = isAdmin
    ? "Manage orders"
    : isCustomer
      ? "My orders"
      : isCad
        ? "My CAD assignments"
        : isProduction
          ? "My production queue"
          : "Final billing & dispatch";

  return (
    <section className="orders-card">
      <div className="orders-heading">
        <div>
          <p className="eyebrow">{workspace}</p>
          <h2>{heading}</h2>
        </div>
        <button className="secondary-button" disabled={orders === null || saving} onClick={() => void refresh()} type="button">
          Refresh
        </button>
      </div>

      {isCustomer && (
        <form className="order-create-form" onSubmit={(event) => void onSubmitOrder(event)}>
          <label htmlFor="order-description">What would you like to order?</label>
          <textarea
            id="order-description"
            maxLength={1000}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Describe the jewelry item, materials, size, and any design details."
            required
            rows={3}
            value={description}
          />
          <button className="primary-button" disabled={saving || !description.trim()} type="submit">
            {saving ? "Submitting…" : "Submit order"}
          </button>
          <p className="orders-muted">Admins are notified when your order is submitted.</p>
        </form>
      )}

      {error && <p className="error-message" role="alert">{error}</p>}
      {orders === null ? (
        <p className="orders-muted">{error ? "Orders could not load." : "Loading orders…"}</p>
      ) : orders.length === 0 ? (
        <p className="orders-empty">
          {isAdmin ? "No orders have been submitted." : "There are no orders assigned to this account yet."}
        </p>
      ) : (
        <div className="orders-list">
          {orders.map((order) => {
            const selectedCadId = selectedCadIds[order.orderId] ?? cadUsers[0]?.userId;
            const selectedProductionId = selectedProductionIds[order.orderId] ?? productionUsers[0]?.userId;
            const selectedBillingId = selectedBillingIds[order.orderId] ?? billingUsers[0]?.userId;
            const nextStage = productionNextStage[order.status];
            return (
              <article className="order-row" key={order.orderId}>
                <div className="order-main">
                  <div className="order-title-line">
                    <h3>Order #{order.orderId}</h3>
                    <span className={`order-status status-${order.status.toLowerCase()}`}>
                      {statusLabels[order.status] ?? order.status}
                    </span>
                  </div>
                  {isAdmin && <p className="orders-muted">Customer ID: {order.customerUserId}</p>}
                  <p className="order-description">{order.description || "No description provided."}</p>
                  <p className="orders-muted">
                    Submitted {new Date(order.createdAt).toLocaleString()}
                    {order.assignedDisplayName && ` · ${order.assignedRole}: ${order.assignedDisplayName}`}
                  </p>
                </div>
                <div className="order-actions">
                  {isAdmin && order.status === "Submitted" && (
                    cadUsers.length === 0 ? (
                      <span className="orders-muted">No active CAD users available.</span>
                    ) : (
                      <>
                        <label className="visually-hidden" htmlFor={`cad-user-${order.orderId}`}>Assign CAD user</label>
                        <select
                          id={`cad-user-${order.orderId}`}
                          onChange={(event) => setSelectedCadIds((current) => ({
                            ...current,
                            [order.orderId]: Number(event.target.value),
                          }))}
                          value={selectedCadId}
                        >
                          {cadUsers.map((cadUser) => (
                            <option key={cadUser.userId} value={cadUser.userId}>{cadUser.displayName}</option>
                          ))}
                        </select>
                        <button
                          className="primary-button"
                          disabled={saving || selectedCadId === undefined}
                          onClick={() => selectedCadId && void performOrderAction(
                            () => assignOrderToCad(accessToken, order.orderId, selectedCadId),
                          )}
                          type="button"
                        >
                          Assign to CAD
                        </button>
                      </>
                    )
                  )}
                  {isAdmin && order.status === "DesignApproved" && (
                    productionUsers.length === 0 ? (
                      <span className="orders-muted">No active production users available.</span>
                    ) : (
                      <>
                        <label className="visually-hidden" htmlFor={`production-user-${order.orderId}`}>Assign production user</label>
                        <select
                          id={`production-user-${order.orderId}`}
                          onChange={(event) => setSelectedProductionIds((current) => ({
                            ...current,
                            [order.orderId]: Number(event.target.value),
                          }))}
                          value={selectedProductionId}
                        >
                          {productionUsers.map((productionUser) => (
                            <option key={productionUser.userId} value={productionUser.userId}>{productionUser.displayName}</option>
                          ))}
                        </select>
                        <button
                          className="primary-button"
                          disabled={saving || selectedProductionId === undefined}
                          onClick={() => selectedProductionId && void performOrderAction(
                            () => assignOrderToProduction(accessToken, order.orderId, selectedProductionId),
                          )}
                          type="button"
                        >
                          Assign to production
                        </button>
                      </>
                    )
                  )}
                  {isCad && ["AssignedToCad", "DesignRejected"].includes(order.status) && (
                    <button
                      className="primary-button"
                      disabled={saving}
                      onClick={() => void performOrderAction(() => submitOrderDesign(accessToken, order.orderId))}
                      type="button"
                    >
                      Submit design for approval
                    </button>
                  )}
                  {isCustomer && order.status === "DesignSubmitted" && (
                    <>
                      <button
                        className="secondary-button"
                        disabled={saving}
                        onClick={() => void performOrderAction(() => decideOrderDesign(accessToken, order.orderId, false))}
                        type="button"
                      >
                        Request revision
                      </button>
                      <button
                        className="primary-button"
                        disabled={saving}
                        onClick={() => void performOrderAction(() => decideOrderDesign(accessToken, order.orderId, true))}
                        type="button"
                      >
                        Approve design
                      </button>
                    </>
                  )}
                  {isProduction && nextStage && (
                    <button
                      className="primary-button"
                      disabled={saving}
                      onClick={() => void performOrderAction(() => advanceProduction(accessToken, order.orderId))}
                      type="button"
                    >
                      Complete {stageLabels[nextStage]}
                    </button>
                  )}
                  {isProduction && order.status === "AssignedToProduction" && (
                    <button
                      className="primary-button"
                      disabled={saving}
                      onClick={() => void performOrderAction(() => advanceProduction(accessToken, order.orderId))}
                      type="button"
                    >
                      Start production
                    </button>
                  )}
                  {isProduction && order.status === "FinishBag" && (
                    billingUsers.length === 0 ? (
                      <span className="orders-muted">No active billing users available.</span>
                    ) : (
                      <>
                        <label className="visually-hidden" htmlFor={`billing-user-${order.orderId}`}>Assign billing user</label>
                        <select
                          id={`billing-user-${order.orderId}`}
                          onChange={(event) => setSelectedBillingIds((current) => ({
                            ...current,
                            [order.orderId]: Number(event.target.value),
                          }))}
                          value={selectedBillingId}
                        >
                          {billingUsers.map((billingUser) => (
                            <option key={billingUser.userId} value={billingUser.userId}>{billingUser.displayName}</option>
                          ))}
                        </select>
                        <button
                          className="primary-button"
                          disabled={saving || selectedBillingId === undefined}
                          onClick={() => selectedBillingId && void performOrderAction(
                            () => assignOrderToBilling(accessToken, order.orderId, selectedBillingId),
                          )}
                          type="button"
                        >
                          Assign to final billing
                        </button>
                      </>
                    )
                  )}
                  {isBilling && order.status === "AssignedToBilling" && (
                    <button
                      className="primary-button"
                      disabled={saving}
                      onClick={() => void performOrderAction(() => completeOrderBilling(accessToken, order.orderId))}
                      type="button"
                    >
                      Complete final billing
                    </button>
                  )}
                  {isBilling && order.status === "ReadyForDispatch" && (
                    <button
                      className="primary-button"
                      disabled={saving}
                      onClick={() => void performOrderAction(() => markOrderDispatched(accessToken, order.orderId))}
                      type="button"
                    >
                      Mark dispatched
                    </button>
                  )}
                </div>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}
