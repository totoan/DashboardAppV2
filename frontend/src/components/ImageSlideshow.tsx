import { useEffect, useState } from "react";

const BACKEND_URL = "http://localhost:5185";

function ImageSlideshow() {
    const [images, setImages] = useState<string[]>([]);
    const [currentImage, setCurrentImage] = useState(0);

    useEffect(() => {
        const loadImages = async () => {
            try {
                const response = await fetch(`${BACKEND_URL}/api/slideshow/images`);
            
                if (!response.ok) {
                    throw new Error("Failed to load slideshow images.");
                }

                const imagePaths: string[] = await response.json();

                const fullUrls = imagePaths.map((path) => `${BACKEND_URL}${path}`);

                setImages(fullUrls);
                setCurrentImage(0);
                } catch (error) {console.error("[Slideshow] Failed to load images:", error);}};

                loadImages();
            }, []);

    useEffect(() => {
        if (images.length <= 1) {
            return;
        }

        const timer = window.setInterval(() => {
            setCurrentImage((current) => {
                return (current + 1) % images.length;
            });
        }, 30000);

        return () => {window.clearInterval(timer);};
    }, [images.length]);

    if (images.length === 0) {
        return (
            <div style={{minHeight: "400px", display: "flex", justifyContent: "center", alignItems: "center", color: "white", backgroundColor: "#111", borderRadius: "8px"}}>
                No Slideshow images found.
            </div>
        );
    }

    return (
        <div style={{width: "100%", height: "100%", minHeight: "400px", display: "flex", justifyContent: "center", alignItems: "center", overflow: "hidden", backgroundColor: "#111", borderRadius: "8px"}}>
            <img
                src={images[currentImage]}
                alt=""
                style={{width: "100%", height: "100%", maxHeight: "600px", objectFit: "contain"}}
            />
        </div>
    );
}

export default ImageSlideshow;
