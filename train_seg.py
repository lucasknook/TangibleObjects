from ultralytics import YOLO

if __name__ == '__main__':
    # Load the YOLOv8 model
    model = YOLO("yolov8s-seg.pt")

    # Train the model
    results = model.train(data="datasets/data.yaml", epochs=500, imgsz=640, device=0, batch=16)

    # Validate on training data
    model.val()