FROM python:3.13-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app

EXPOSE 8000

# Un solo proceso: los eventos viven en memoria, y con varios procesos cada uno
# tendría su propia lista y su propio seq.
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
