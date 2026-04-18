type NetworkTileProps = {
    title: string;
    valueOut: string;
    valueIn: string;
};

function NetworkTile({ title, valueOut, valueIn }: NetworkTileProps) {
    return (
        <div style={{
            backgroundColor: "#1e1e1e",
            border: "2px solid Red",
            borderRadius: "10px",
            padding: "16px",
            color: "white",
        }}>
            <div style={{ fontSize: "18px", marginBottom: "8px" }}>{title}</div>
            <div style={{ fontSize: "24px" }}>↑{valueOut}</div>
            <div style={{ fontSize: "24px" }}>↓{valueIn}</div>
        </div>
    );
}

export default NetworkTile;