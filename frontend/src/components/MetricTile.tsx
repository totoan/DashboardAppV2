type MetricTileProps = {
    title: string;
    value: string;
};

function MetricTile({ title, value }: MetricTileProps) {
    return (
        <div style={{
            backgroundColor: "#1e1e1e",
            border: "2px solid Red",
            borderRadius: "10px",
            padding: "16px",
            color: "white",
        }}>
            <div style={{ fontSize: "18px", marginBottom: "8px" }}>{title}</div>
            <div style={{ fontSize: "24px" }}>{value}</div>
        </div>
    );
}

export default MetricTile;