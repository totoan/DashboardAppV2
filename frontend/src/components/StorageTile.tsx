type StorageDrive = {
    name: string;
    size: number;
    inUse: number;
};

type StorageTileProps = {
    title: string;
    drives: StorageDrive[];
};

function StorageTile({ title, drives }: StorageTileProps) {
    return (
        <div style={{
            backgroundColor: "#1e1e1e",
            border: "2px solid Red",
            borderRadius: "10px",
            padding: "16px",
            color: "white",
            }}
        >
            <div style={{ fontSize: "18px", marginBottom: "8px" }}>{title}</div>

            {drives.map((drive) => {
                return (
                    <div key={drive.name} style={{ marginBottom: "1px", fontSize: "16px"}}>
                        <div>
                            {drive.name}
                            {(drive.inUse / 1000000000).toFixed(1)} GB /{" "}
                            {(drive.size / 1000000000).toFixed(1)} GB
                        </div>
                    </div>
                )
            })}
        </div>
    );
}

export default StorageTile;